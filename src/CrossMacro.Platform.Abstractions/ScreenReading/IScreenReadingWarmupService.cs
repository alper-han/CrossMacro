
namespace CrossMacro.Platform.Abstractions.ScreenReading;

public interface IScreenReadingWarmupService
{
    public Task WarmUpPortalSessionAsync(CancellationToken cancellationToken = default);
}
