
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeKWinScreenShotSupportProbe(WaylandSupportResult support) : IKWinScreenShotSupportProbe
{
    private readonly WaylandSupportResult _support = support;

    public WaylandSupportResult ProbeSupport() => _support;
}
