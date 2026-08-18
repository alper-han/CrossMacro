
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public readonly record struct KWinScreenShotSupportResult
{
    private KWinScreenShotSupportResult(bool isSupported, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (!isSupported && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Unavailable KWin screenshot probes require a message.", nameof(errorMessage));
        }

        IsSupported = isSupported;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSupported { get; }
    public ScreenReadErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }

    public static KWinScreenShotSupportResult Supported() => new(isSupported: true, errorKind: null, errorMessage: null);
    public static KWinScreenShotSupportResult Unsupported(string errorMessage) => new(isSupported: false, ScreenReadErrorKind.BackendUnavailable, errorMessage);
    public static KWinScreenShotSupportResult Failure(ScreenReadErrorKind errorKind, string errorMessage) => new(isSupported: false, errorKind, errorMessage);
}
