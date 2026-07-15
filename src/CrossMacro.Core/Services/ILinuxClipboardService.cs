
namespace CrossMacro.Core.Services;

public interface ILinuxClipboardService : IClipboardService
{
    public Task InitializeAsync(CancellationToken cancellationToken = default);
}
