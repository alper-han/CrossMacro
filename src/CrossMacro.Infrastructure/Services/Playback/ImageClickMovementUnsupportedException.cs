
namespace CrossMacro.Infrastructure.Services.Playback;

public sealed class ImageClickMovementUnsupportedException : InvalidOperationException
{
    public ImageClickMovementUnsupportedException()
        : base("Image click movement is not supported.")
    {
    }

    public ImageClickMovementUnsupportedException(string message)
        : base(message)
    {
    }

    public ImageClickMovementUnsupportedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
