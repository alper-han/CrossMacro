
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeKWinScreenShotSupportProbe(KWinScreenShotSupportResult support) : IKWinScreenShotSupportProbe
{
    private readonly KWinScreenShotSupportResult _support = support;

    public KWinScreenShotSupportResult ProbeSupport() => _support;
}
