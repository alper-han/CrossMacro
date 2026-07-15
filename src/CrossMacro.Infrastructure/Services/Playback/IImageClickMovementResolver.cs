
namespace CrossMacro.Infrastructure.Services.Playback;

public interface IImageClickMovementResolver
{
    Task<ImageClickMovementResolution> ResolveAsync(
        IInputSimulator inputSimulator,
        ScreenPoint target,
        CancellationToken cancellationToken);
}
