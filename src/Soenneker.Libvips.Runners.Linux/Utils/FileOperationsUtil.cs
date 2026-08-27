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

namespace Soenneker.Libvips.Runners.Linux.Utils;

/// <inheritdoc cref="IFileOperationsUtil"/>
public sealed class FileOperationsUtil : IFileOperationsUtil
{
    private const string Owner = "kleisauke";
    private const string Repository = "libvips-packaging";
    private const string AssetPattern = "linux-x64.tar.gz";
    private const string NativeFileName = "libvips.so.42";

    private readonly ILogger<FileOperationsUtil> _logger;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IGitHubRepositoriesReleasesUtil _releasesUtil;
    private readonly ITarUtil _tarUtil;

    public FileOperationsUtil(ILogger<FileOperationsUtil> logger, IDirectoryUtil directoryUtil,
        IGitHubRepositoriesReleasesUtil releasesUtil, ITarUtil tarUtil)
    {
        _logger = logger;
        _directoryUtil = directoryUtil;
        _releasesUtil = releasesUtil;
        _tarUtil = tarUtil;
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
        CopyRequiredFile(extractionDirectory, stageDirectory, Path.Combine("lib", NativeFileName));
        CopyRequiredFile(extractionDirectory, stageDirectory, "THIRD-PARTY-NOTICES.md");
        CopyRequiredFile(extractionDirectory, stageDirectory, "versions.json");

        _logger.LogInformation("Prepared Linux x64 libvips runtime at {StageDirectory}", stageDirectory);
        return stageDirectory;
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

    private static void CopyRequiredFile(string sourceRoot, string destinationRoot, string relativePath)
    {
        string source = Path.Combine(sourceRoot, relativePath);
        if (!File.Exists(source))
            throw new FileNotFoundException($"Expected libvips release file was not found: {relativePath}", source);

        string destination = Path.Combine(destinationRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, true);
    }
}
