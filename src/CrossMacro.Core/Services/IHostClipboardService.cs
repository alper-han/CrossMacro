
namespace CrossMacro.Core.Services;

public interface IHostClipboardService : IClipboardService
{
    public Task InitializeAsync(CancellationToken cancellationToken = default);
}
