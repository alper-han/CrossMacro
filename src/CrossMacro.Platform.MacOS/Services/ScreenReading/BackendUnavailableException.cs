
namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal sealed class BackendUnavailableException : InvalidOperationException
{
    public BackendUnavailableException()
    {
    }

    public BackendUnavailableException(string? message)
        : base(message)
    {
    }

    public BackendUnavailableException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
