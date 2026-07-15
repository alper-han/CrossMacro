
namespace CrossMacro.Core.Services;

public interface IHostClipboardService : IClipboardService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
