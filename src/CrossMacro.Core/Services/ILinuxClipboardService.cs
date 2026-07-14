using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Core.Services;

public interface ILinuxClipboardService : IClipboardService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
