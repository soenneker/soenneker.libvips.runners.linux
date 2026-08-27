using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soenneker.Compression.Tar.Abstract;
using Soenneker.GitHub.Repositories.Releases.Abstract;
using Soenneker.Libvips.Runners.Linux.Utils.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.Process.Abstract;

namespace Soenneker.Libvips.Runners.Linux.Utils;

/// <inheritdoc cref="IFileOperationsUtil"/>
public sealed class FileOperationsUtil : IFileOperationsUtil
{
    private const string Owner = "kleisauke";
    private const string Repository = "libvips-packaging";
    private const string AssetPattern = "linux-x64.tar.gz";
    private const string InstallTools = "sudo apt-get update && sudo apt-get install -y --no-install-recommends libvips-tools";

    private readonly ILogger<FileOperationsUtil> _logger;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IGitHubRepositoriesReleasesUtil _releasesUtil;
    private readonly ITarUtil _tarUtil;
    private readonly IProcessUtil _processUtil;

    public FileOperationsUtil(ILogger<FileOperationsUtil> logger, IDirectoryUtil directoryUtil,
        IGitHubRepositoriesReleasesUtil releasesUtil, ITarUtil tarUtil, IProcessUtil processUtil)
    {
        _logger = logger;
        _directoryUtil = directoryUtil;
        _releasesUtil = releasesUtil;
        _tarUtil = tarUtil;
        _processUtil = processUtil;
    }

    public async ValueTask<string> Process(CancellationToken cancellationToken = default)
    {
        string downloadDirectory = await _directoryUtil.CreateTempDirectory(cancellationToken);
        string? asset = await _releasesUtil.DownloadReleaseAssetByNamePattern(Owner, Repository, downloadDirectory, [AssetPattern], cancellationToken);

        if (asset is null)
            throw new FileNotFoundException($"Could not find a stable {Repository} release asset matching '{AssetPattern}'.");

        string tarFile = Path.Combine(downloadDirectory, Path.GetFileNameWithoutExtension(asset));
        await DecompressGzip(asset, tarFile, cancellationToken);

        string extractionDirectory = await _directoryUtil.CreateTempDirectory(cancellationToken);
        await _tarUtil.Extract(tarFile, extractionDirectory, cancellationToken);

        string stageDirectory = await _directoryUtil.CreateTempDirectory(cancellationToken);
        CopyDirectory(extractionDirectory, stageDirectory);

        await _processUtil.BashRun(InstallTools, stageDirectory, cancellationToken: cancellationToken);

        string binDirectory = Path.Combine(stageDirectory, "bin");
        Directory.CreateDirectory(binDirectory);
        File.Copy("/usr/bin/vips", Path.Combine(binDirectory, "vips"), true);
        File.Copy("/usr/bin/vipsheader", Path.Combine(binDirectory, "vipsheader"), true);

        await WriteLauncher(stageDirectory, "vips", cancellationToken);
        await WriteLauncher(stageDirectory, "vipsheader", cancellationToken);

        await _processUtil.BashRun("chmod +x vips.sh vipsheader.sh bin/vips bin/vipsheader", stageDirectory, cancellationToken: cancellationToken);
        await _processUtil.BashRun("./vips.sh --version", stageDirectory, cancellationToken: cancellationToken);

        _logger.LogInformation("Prepared Linux x64 libvips runtime at {StageDirectory}", stageDirectory);
        return stageDirectory;
    }

    private static Task WriteLauncher(string stageDirectory, string executable, CancellationToken cancellationToken)
    {
        string launcher = Path.Combine(stageDirectory, $"{executable}.sh");
        return File.WriteAllTextAsync(launcher,
            $"#!/bin/bash\nset -euo pipefail\nDIR=$(dirname \"$(readlink -f \"$0\")\")\nexport LD_LIBRARY_PATH=\"$DIR/lib:${{LD_LIBRARY_PATH:-}}\"\nexec \"$DIR/bin/{executable}\" \"$@\"\n",
            cancellationToken);
    }

    private static async ValueTask DecompressGzip(string source, string destination, CancellationToken cancellationToken)
    {
        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var gzipStream = new GZipStream(sourceStream, CompressionMode.Decompress);
        await using var destinationStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await gzipStream.CopyToAsync(destinationStream, cancellationToken);
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        foreach (string directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));

        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }
}
