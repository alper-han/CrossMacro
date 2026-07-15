using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.X11;

public readonly record struct X11ScreenCaptureSupportResult
{
    private X11ScreenCaptureSupportResult(bool isSupported, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (!isSupported && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Unavailable X11 screen capture probes require a message.", nameof(errorMessage));
        }

        IsSupported = isSupported;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSupported { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static X11ScreenCaptureSupportResult Supported() => new(isSupported: true, errorKind: null, errorMessage: null);

    public static X11ScreenCaptureSupportResult Unsupported(string errorMessage) =>
        new(isSupported: false, ScreenReadErrorKind.BackendUnavailable, errorMessage);

    public static X11ScreenCaptureSupportResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new(isSupported: false, errorKind, errorMessage);
}
