using Microsoft.Extensions.DependencyInjection;
using Soenneker.Compression.Tar.Registrars;
using Soenneker.GitHub.Repositories.Releases.Registrars;
using Soenneker.Libvips.Runners.Linux.Utils;
using Soenneker.Libvips.Runners.Linux.Utils.Abstract;
using Soenneker.Managers.Runners.Registrars;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.Process.Registrars;

namespace Soenneker.Libvips.Runners.Linux;

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddHostedService<ConsoleHostedService>()
            .AddSingleton<IFileOperationsUtil, FileOperationsUtil>()
            .AddDirectoryUtilAsSingleton()
            .AddProcessUtilAsSingleton()
            .AddTarUtilAsSingleton()
            .AddGitHubRepositoriesReleasesUtilAsSingleton()
            .AddRunnersManagerAsSingleton();
    }
}
