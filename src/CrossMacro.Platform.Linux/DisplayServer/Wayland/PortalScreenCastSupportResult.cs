
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public readonly record struct PortalScreenCastSupportResult
{
    private PortalScreenCastSupportResult(bool isSupported, ScreenReadErrorKind? errorKind, string? errorMessage, string? diagnostic)
    {
        if (!isSupported && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Unavailable portal probes require a message.", nameof(errorMessage));
        }

        IsSupported = isSupported;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
        Diagnostic = diagnostic;
    }

    public bool IsSupported { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public string? Diagnostic { get; }

    public static PortalScreenCastSupportResult Supported(string? diagnostic = null) => new(isSupported: true, errorKind: null, errorMessage: null, diagnostic);
    public static PortalScreenCastSupportResult Unsupported(string errorMessage, string? diagnostic = null) => new(isSupported: false, ScreenReadErrorKind.BackendUnavailable, errorMessage, diagnostic);
    public static PortalScreenCastSupportResult Failure(ScreenReadErrorKind errorKind, string errorMessage, string? diagnostic = null) => new(isSupported: false, errorKind, errorMessage, diagnostic);
}
