
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeExtImageCopyProbe(ExtImageCopySupportResult support) : IExtImageCopySupportProbe
{
    private readonly ExtImageCopySupportResult _support = support;

    public ExtImageCopySupportResult ProbeSupport() => _support;
}
