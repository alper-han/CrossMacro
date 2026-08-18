namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Indicates that a platform input backend did not accept an injected input event.
/// </summary>
public sealed class InputInjectionFailedException(
    string? message,
    int nativeErrorCode,
    Exception? innerException) : InvalidOperationException(message, innerException)
{
    public InputInjectionFailedException()
        : this(message: null, nativeErrorCode: 0)
    {
    }

    public InputInjectionFailedException(string? message)
        : this(message, nativeErrorCode: 0)
    {
    }

    public InputInjectionFailedException(string? message, Exception? innerException)
        : this(message, nativeErrorCode: 0, innerException)
    {
    }

    public InputInjectionFailedException(string? message, int nativeErrorCode)
        : this(message, nativeErrorCode, innerException: null)
    {
    }

    public int NativeErrorCode { get; } = nativeErrorCode;
}
