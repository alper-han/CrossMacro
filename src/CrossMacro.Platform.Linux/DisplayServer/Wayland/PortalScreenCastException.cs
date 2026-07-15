
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class PortalScreenCastException : Exception
{
    public PortalScreenCastException(ScreenReadErrorKind errorKind, string message)
        : base(message)
    {
        ErrorKind = errorKind;
    }

    public ScreenReadErrorKind ErrorKind { get; }
}
