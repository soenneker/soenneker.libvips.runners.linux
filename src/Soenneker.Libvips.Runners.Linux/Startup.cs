using Microsoft.Extensions.DependencyInjection;
using Soenneker.GitHub.Repositories.Releases.Registrars;
using Soenneker.Libvips.Runners.Linux.Utils;
using Soenneker.Libvips.Runners.Linux.Utils.Abstract;
using Soenneker.Managers.Runners.Registrars;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.File.Download.Registrars;
using Soenneker.Utils.File.Registrars;
using Soenneker.Utils.Process.Registrars;
namespace Soenneker.Libvips.Runners.Linux;

public static class Startup
{
    /// <summary>
    /// Registers the services required by the application host.
    /// </summary>
    /// <param name="services">Service collection that receives the registration.</param>
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddHostedService<ConsoleHostedService>()
            .AddSingleton<IFileOperationsUtil, FileOperationsUtil>()
            .AddDirectoryUtilAsSingleton()
            .AddFileDownloadUtilAsSingleton()
            .AddFileUtilAsSingleton()
            .AddProcessUtilAsSingleton()
            .AddGitHubRepositoriesReleasesUtilAsSingleton()
            .AddRunnersManagerAsSingleton();
    }
}
