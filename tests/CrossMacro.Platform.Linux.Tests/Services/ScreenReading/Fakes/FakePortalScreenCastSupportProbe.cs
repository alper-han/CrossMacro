using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakePortalScreenCastSupportProbe : IPortalScreenCastSupportProbe
{
    private readonly PortalScreenCastSupportResult _support;

    public FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult support)
    {
        _support = support;
    }

    public PortalScreenCastSupportResult ProbeSupport() => _support;
}
