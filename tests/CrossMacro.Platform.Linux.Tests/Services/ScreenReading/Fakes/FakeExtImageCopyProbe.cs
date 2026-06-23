using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeExtImageCopyProbe : IExtImageCopySupportProbe
{
    private readonly ExtImageCopySupportResult _support;

    public FakeExtImageCopyProbe(ExtImageCopySupportResult support)
    {
        _support = support;
    }

    public ExtImageCopySupportResult ProbeSupport() => _support;
}
