
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeWlrScreencopySupportProbe(WlrScreencopySupportResult support) : IWlrScreencopySupportProbe
{
    private readonly WlrScreencopySupportResult _support = support;

    public WlrScreencopySupportResult ProbeSupport() => _support;
}
