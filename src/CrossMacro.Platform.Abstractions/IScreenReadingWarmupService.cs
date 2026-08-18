
namespace CrossMacro.Platform.Abstractions;

public interface IScreenReadingWarmupService
{
    public Task WarmUpPortalSessionAsync(CancellationToken cancellationToken = default);
}
