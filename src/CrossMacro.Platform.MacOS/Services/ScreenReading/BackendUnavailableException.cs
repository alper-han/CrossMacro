
namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal sealed class BackendUnavailableException : InvalidOperationException
{
    public BackendUnavailableException() { /* Empty */ }

    public BackendUnavailableException(string? message)
        : base(message) { /* Empty */ }

    public BackendUnavailableException(string? message, Exception? innerException)
        : base(message, innerException) { /* Empty */ }
}
