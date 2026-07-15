using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeKWinScreenShotSupportProbe : IKWinScreenShotSupportProbe
{
    private readonly KWinScreenShotSupportResult _support;

    public FakeKWinScreenShotSupportProbe(KWinScreenShotSupportResult support)
    {
        _support = support;
    }

    public KWinScreenShotSupportResult ProbeSupport() => _support;
}
