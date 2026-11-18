
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeExtImageCopyProbe(WaylandSupportResult support) : IExtImageCopySupportProbe
{
    private readonly WaylandSupportResult _support = support;

    public WaylandSupportResult ProbeSupport() => _support;
}
