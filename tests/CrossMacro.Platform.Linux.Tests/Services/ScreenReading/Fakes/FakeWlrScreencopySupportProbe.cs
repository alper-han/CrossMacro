
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeWlrScreencopySupportProbe(WaylandSupportResult support) : IWlrScreencopySupportProbe
{
    private readonly WaylandSupportResult _support = support;

    public WaylandSupportResult ProbeSupport() => _support;
}
