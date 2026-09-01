using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Soenneker.GitHub.Repositories.Releases.Abstract;
using Soenneker.Libvips.Runners.Linux.Utils.Abstract;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.File.Download.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.Process.Abstract;

namespace Soenneker.Libvips.Runners.Linux.Utils;

/// <inheritdoc cref="IFileOperationsUtil"/>
public sealed class FileOperationsUtil : IFileOperationsUtil
{
    private const string Owner = "kleisauke";
    private const string Repository = "libvips-packaging";
    private const string AssetPattern = "linux-x64.tar.gz";
    private const string InstallTools = "sudo apt-get update && sudo apt-get install -y --no-install-recommends build-essential pkg-config libglib2.0-dev";

    private readonly ILogger<FileOperationsUtil> _logger;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly IGitHubRepositoriesReleasesUtil _releasesUtil;
    private readonly IProcessUtil _processUtil;
    private readonly IFileDownloadUtil _fileDownloadUtil;
    private readonly IFileUtil _fileUtil;

    public FileOperationsUtil(ILogger<FileOperationsUtil> logger, IDirectoryUtil directoryUtil,
        IGitHubRepositoriesReleasesUtil releasesUtil, IProcessUtil processUtil, IFileDownloadUtil fileDownloadUtil, IFileUtil fileUtil)
    {
        _logger = logger;
        _directoryUtil = directoryUtil;
        _releasesUtil = releasesUtil;
        _processUtil = processUtil;
        _fileDownloadUtil = fileDownloadUtil;
        _fileUtil = fileUtil;
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
        await _processUtil.BashRun($"tar -xf \"{tarFile}\" -C \"{extractionDirectory}\"", downloadDirectory,
            cancellationToken: cancellationToken);

        string stageDirectory = extractionDirectory;

        await _processUtil.BashRun(InstallTools, stageDirectory, cancellationToken: cancellationToken);

        string binDirectory = Path.Combine(stageDirectory, "bin");
        await _directoryUtil.Create(binDirectory, log: false, cancellationToken);

        string versionsJson = await _fileUtil.Read(Path.Combine(stageDirectory, "versions.json"), log: false, cancellationToken);
        using JsonDocument versions = JsonDocument.Parse(versionsJson);
        string version = versions.RootElement.GetProperty("vips").GetString()
                         ?? throw new InvalidOperationException("The libvips distribution did not specify its vips version.");

        await BuildTool("vips", version, stageDirectory, binDirectory, cancellationToken);
        await BuildTool("vipsheader", version, stageDirectory, binDirectory, cancellationToken);

        await WriteLauncher(stageDirectory, "vips", cancellationToken);
        await WriteLauncher(stageDirectory, "vipsheader", cancellationToken);

        await _processUtil.BashRun("chmod +x vips.sh vipsheader.sh bin/vips bin/vipsheader", stageDirectory, cancellationToken: cancellationToken);
        await _processUtil.BashRun("./vips.sh --version", stageDirectory, cancellationToken: cancellationToken);

        _logger.LogInformation("Prepared Linux x64 libvips runtime at {StageDirectory}", stageDirectory);
        return stageDirectory;
    }

    private async ValueTask BuildTool(string tool, string version, string stageDirectory, string binDirectory, CancellationToken cancellationToken)
    {
        string buildDirectory = Path.Combine(stageDirectory, "build", tool);
        await _directoryUtil.Create(buildDirectory, log: false, cancellationToken);

        string sourcePath = Path.Combine(buildDirectory, $"{tool}.c");
        string? source = await _fileDownloadUtil.Download($"https://raw.githubusercontent.com/libvips/libvips/v{version}/tools/{tool}.c",
            filePath: sourcePath, log: false, cancellationToken: cancellationToken);

        if (source is null)
            throw new FileNotFoundException($"Could not download {tool}.c for libvips {version}.");

        string internalHeaderPath = Path.Combine(buildDirectory, "internal.h");
        string? header = await _fileDownloadUtil.Download(
            $"https://raw.githubusercontent.com/libvips/libvips/v{version}/libvips/include/vips/internal.h",
            filePath: internalHeaderPath, log: false, cancellationToken: cancellationToken);

        if (header is null || !await _fileUtil.Exists(internalHeaderPath, cancellationToken))
            throw new FileNotFoundException($"Could not download internal.h for libvips {version}.");

        string sourceText = await _fileUtil.Read(sourcePath, log: false, cancellationToken);
        sourceText = sourceText.Replace("#include <vips/internal.h>", "#include \"internal.h\"", StringComparison.Ordinal);

        // The G_IS_PARAM_SPEC_* macros reference g_param_spec_types directly. Linking that
        // data symbol from the system GObject runtime would load a second GLib beside the
        // one bundled into libvips, so use the argument value type instead.
        sourceText = sourceText
            .Replace("G_IS_PARAM_SPEC_ENUM(pspec)", "G_TYPE_FUNDAMENTAL(type) == G_TYPE_ENUM", StringComparison.Ordinal)
            .Replace("G_IS_PARAM_SPEC_BOOLEAN(pspec)", "G_TYPE_FUNDAMENTAL(type) == G_TYPE_BOOLEAN", StringComparison.Ordinal)
            .Replace("G_IS_PARAM_SPEC_DOUBLE(pspec)", "G_TYPE_FUNDAMENTAL(type) == G_TYPE_DOUBLE", StringComparison.Ordinal)
            .Replace("G_IS_PARAM_SPEC_INT(pspec)", "G_TYPE_FUNDAMENTAL(type) == G_TYPE_INT", StringComparison.Ordinal)
            .Replace("G_IS_PARAM_SPEC_OBJECT(pspec)", "G_TYPE_FUNDAMENTAL(type) == G_TYPE_OBJECT", StringComparison.Ordinal);

        await _fileUtil.Write(sourcePath, sourceText, log: false, cancellationToken);

        string outputPath = Path.Combine(binDirectory, tool);
        string command = $"gcc -O2 -DGETTEXT_PACKAGE='\"vips\"' -I\"{buildDirectory}\" -I\"{stageDirectory}/include\" " +
                         $"$(pkg-config --cflags glib-2.0 gobject-2.0 gio-2.0) \"{sourcePath}\" \"{stageDirectory}/lib/libvips.so.42\" " +
                         $"-Wl,-rpath,'$ORIGIN/../lib' -o \"{outputPath}\"";

        await _processUtil.BashRun(command, stageDirectory, cancellationToken: cancellationToken);
    }

    private Task WriteLauncher(string stageDirectory, string executable, CancellationToken cancellationToken)
    {
        string launcher = Path.Combine(stageDirectory, $"{executable}.sh");
        return _fileUtil.Write(launcher,
            $"#!/bin/bash\nset -euo pipefail\nDIR=$(dirname \"$(readlink -f \"$0\")\")\nexport LD_LIBRARY_PATH=\"$DIR/lib:${{LD_LIBRARY_PATH:-}}\"\nexec \"$DIR/bin/{executable}\" \"$@\"\n",
            log: false, cancellationToken);
    }

    private async ValueTask DecompressGzip(string source, string destination, CancellationToken cancellationToken)
    {
        await using FileStream sourceStream = _fileUtil.OpenRead(source, log: false);
        await using var gzipStream = new GZipStream(sourceStream, CompressionMode.Decompress);
        await using FileStream destinationStream = _fileUtil.OpenWrite(destination, log: false);

        await gzipStream.CopyToAsync(destinationStream, cancellationToken);
    }

}
