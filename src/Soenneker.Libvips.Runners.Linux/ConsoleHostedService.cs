using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Soenneker.Libvips.Runners.Linux.Utils.Abstract;
using Soenneker.Managers.Runners.Abstract;
namespace Soenneker.Libvips.Runners.Linux;
public sealed class ConsoleHostedService : IHostedService
{
    private readonly ILogger<ConsoleHostedService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IFileOperationsUtil _fileOperationsUtil;
    private readonly IRunnersManager _runnersManager;
    private int? _exitCode;

    public ConsoleHostedService(ILogger<ConsoleHostedService> logger, IHostApplicationLifetime appLifetime,
        IFileOperationsUtil fileOperationsUtil, IRunnersManager runnersManager)
    {
        _logger = logger;
        _appLifetime = appLifetime;
        _fileOperationsUtil = fileOperationsUtil;
        _runnersManager = runnersManager;
    }

    /// <summary>
    /// Starts the Console Hosted Service and begins its background work.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes after the Console Hosted Service has started.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _appLifetime.ApplicationStarted.Register(() => Task.Run(async () =>
        {
            try
            {
                string stageDirectory = await _fileOperationsUtil.Process(cancellationToken);
                await _runnersManager.PushIfChangesNeededForDirectory(Path.Combine(Constants.RuntimeIdentifier, "libvips"), stageDirectory,
                    Constants.Library, $"https://github.com/soenneker/{Constants.Library}", false, cancellationToken);
                _exitCode = 0;
            }
            catch (Exception exception)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();

                _logger.LogError(exception, "Could not update {Library}", Constants.Library);
                _exitCode = 1;
            }
            finally
            {
                _appLifetime.StopApplication();
            }
        }));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the Console Hosted Service and waits for its background work to finish.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes after the Console Hosted Service has stopped.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Environment.ExitCode = _exitCode.GetValueOrDefault(-1);
        return Task.CompletedTask;
    }
}
