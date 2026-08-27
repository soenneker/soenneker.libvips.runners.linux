using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Libvips.Runners.Linux.Utils.Abstract;

public interface IFileOperationsUtil
{
    ValueTask<string> Process(CancellationToken cancellationToken = default);
}
