using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IWlrScreencopySupportProbe
{
    WlrScreencopySupportResult ProbeSupport();
}
