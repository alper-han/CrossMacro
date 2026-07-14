using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Core.Services;

public interface IHostClipboardService : IClipboardService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
