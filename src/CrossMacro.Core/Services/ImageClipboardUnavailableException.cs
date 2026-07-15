
namespace CrossMacro.Core.Services;

public sealed class ImageClipboardUnavailableException : InvalidOperationException
{
    public ImageClipboardUnavailableException()
    {
    }

    public ImageClipboardUnavailableException(string? message)
        : base(message)
    {
    }

    public ImageClipboardUnavailableException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
