
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public readonly record struct PortalScreenCastSessionResult
{
    private PortalScreenCastSessionResult(PortalScreenCastSession? session, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (session is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed portal sessions require a message.", nameof(errorMessage));
        }

        Session = session;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;
    public PortalScreenCastSession? Session { get; }
    public ScreenReadErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }
    public static PortalScreenCastSessionResult Success(PortalScreenCastSession session) => new(session ?? throw new ArgumentNullException(nameof(session)), errorKind: null, errorMessage: null);
    public static PortalScreenCastSessionResult Failure(ScreenReadErrorKind errorKind, string errorMessage) => new(session: null, errorKind, errorMessage);
}
