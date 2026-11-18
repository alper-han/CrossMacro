
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Outcome of probing one Wayland screen backend.
/// </summary>
public readonly record struct WaylandSupportResult
{
    private WaylandSupportResult(bool isSupported, ScreenReadErrorKind? errorKind, string? errorMessage, string? diagnostic)
    {
        if (!isSupported && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Unavailable Wayland screen backends require a message.", nameof(errorMessage));
        }

        IsSupported = isSupported;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
        Diagnostic = diagnostic;
    }

    public bool IsSupported { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    /// <summary>Backend-specific detail for logs; never shown as the user-facing reason.</summary>
    public string? Diagnostic { get; }

    public static WaylandSupportResult Supported(string? diagnostic = null) =>
        new(isSupported: true, errorKind: null, errorMessage: null, diagnostic);

    public static WaylandSupportResult Unsupported(string errorMessage, string? diagnostic = null) =>
        new(isSupported: false, ScreenReadErrorKind.BackendUnavailable, errorMessage, diagnostic);

    public static WaylandSupportResult Failure(ScreenReadErrorKind errorKind, string errorMessage, string? diagnostic = null) =>
        new(isSupported: false, errorKind, errorMessage, diagnostic);
}
