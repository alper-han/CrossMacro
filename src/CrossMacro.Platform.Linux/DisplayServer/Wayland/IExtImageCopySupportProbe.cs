using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IExtImageCopySupportProbe
{
    ExtImageCopySupportResult ProbeSupport();
}
