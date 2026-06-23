using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeWlrScreencopySupportProbe : IWlrScreencopySupportProbe
{
    private readonly WlrScreencopySupportResult _support;

    public FakeWlrScreencopySupportProbe(WlrScreencopySupportResult support)
    {
        _support = support;
    }

    public WlrScreencopySupportResult ProbeSupport() => _support;
}
