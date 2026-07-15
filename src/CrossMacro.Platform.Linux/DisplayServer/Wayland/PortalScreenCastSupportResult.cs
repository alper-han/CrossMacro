
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public readonly record struct PortalScreenCastSupportResult
{
    private PortalScreenCastSupportResult(bool isSupported, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (!isSupported && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Unavailable portal probes require a message.", nameof(errorMessage));
        }

        IsSupported = isSupported;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSupported { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static PortalScreenCastSupportResult Supported() => new(isSupported: true, errorKind: null, errorMessage: null);
    public static PortalScreenCastSupportResult Unsupported(string errorMessage) => new(isSupported: false, ScreenReadErrorKind.BackendUnavailable, errorMessage);
    public static PortalScreenCastSupportResult Failure(ScreenReadErrorKind errorKind, string errorMessage) => new(isSupported: false, errorKind, errorMessage);
}
