
namespace CrossMacro.Infrastructure.Services.Playback;

public sealed class ImageClickMovementUnsupportedException : InvalidOperationException
{
    public ImageClickMovementUnsupportedException(string message)
        : base(message)
    {
    }
}
