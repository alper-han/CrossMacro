
namespace CrossMacro.Core.Services;

public sealed class ImageClipboardUnavailableException : InvalidOperationException
{
    public ImageClipboardUnavailableException() { /* Empty */ }

    public ImageClipboardUnavailableException(string? message)
        : base(message) { /* Empty */ }

    public ImageClipboardUnavailableException(string? message, Exception? innerException)
        : base(message, innerException) { /* Empty */ }
}
