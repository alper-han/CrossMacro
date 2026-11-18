
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakePortalScreenCastSupportProbe(WaylandSupportResult support) : IPortalScreenCastSupportProbe
{
    private readonly WaylandSupportResult _support = support;

    public WaylandSupportResult ProbeSupport() => _support;
}
