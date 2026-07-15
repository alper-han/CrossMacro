using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public readonly record struct ExtImageCopySupportResult
{
    private ExtImageCopySupportResult(bool isSupported, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (!isSupported && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Unavailable ext-image-copy probes require a message.", nameof(errorMessage));
        }

        IsSupported = isSupported;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSupported { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static ExtImageCopySupportResult Supported() => new(isSupported: true, errorKind: null, errorMessage: null);

    public static ExtImageCopySupportResult Unsupported(string errorMessage) =>
        new(isSupported: false, ScreenReadErrorKind.BackendUnavailable, errorMessage);

    public static ExtImageCopySupportResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new(isSupported: false, errorKind, errorMessage);
}
