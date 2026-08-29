using System.Threading;
using System.Threading.Tasks;
namespace Soenneker.Libvips.Runners.Linux.Utils.Abstract;

public interface IFileOperationsUtil
{
    /// <summary>
    /// Processes the pending work managed by the File Operations.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task whose result is the text returned by process.</returns>
    ValueTask<string> Process(CancellationToken cancellationToken = default);
}
