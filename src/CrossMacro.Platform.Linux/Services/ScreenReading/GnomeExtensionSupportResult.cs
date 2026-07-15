using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public readonly record struct GnomeExtensionSupportResult
{
    private GnomeExtensionSupportResult(bool isSupported, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        IsSupported = isSupported;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSupported { get; }
    public ScreenReadErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }

    public static GnomeExtensionSupportResult Supported() => new(isSupported: true, errorKind: null, errorMessage: null);
    public static GnomeExtensionSupportResult Failure(ScreenReadErrorKind errorKind, string errorMessage) => new(isSupported: false, errorKind, errorMessage);
}
