
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class PortalScreenCastException : Exception
{
    public PortalScreenCastException()
        : this(ScreenReadErrorKind.CaptureFailed, "Portal screen cast error occurred.") { /* Empty */ }

    public PortalScreenCastException(string message)
        : this(ScreenReadErrorKind.CaptureFailed, message) { /* Empty */ }

    public PortalScreenCastException(string message, Exception innerException)
        : this(ScreenReadErrorKind.CaptureFailed, message, innerException) { /* Empty */ }

    public PortalScreenCastException(ScreenReadErrorKind errorKind, string message)
        : base(message)
    {
        ErrorKind = errorKind;
    }

    public PortalScreenCastException(ScreenReadErrorKind errorKind, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorKind = errorKind;
    }

    public ScreenReadErrorKind ErrorKind { get; }
}
