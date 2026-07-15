
namespace CrossMacro.Infrastructure.Services.Playback;

public interface IImageClickMovementResolver
{
    public Task<ImageClickMovementResolution> ResolveAsync(
        IInputSimulator inputSimulator,
        ScreenPoint target,
        CancellationToken cancellationToken);
}
