
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakePortalScreenCastSupportProbe(PortalScreenCastSupportResult support) : IPortalScreenCastSupportProbe
{
    private readonly PortalScreenCastSupportResult _support = support;

    public PortalScreenCastSupportResult ProbeSupport() => _support;
}
