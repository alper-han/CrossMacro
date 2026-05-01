using CrossMacro.Platform.Linux.Services.Keyboard;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services.Keyboard;

public sealed class LinuxLayoutDetectorTests
{
    [Fact]
    public void TryResolveKdeLayout_ReturnsShortName_ForValidIndex()
    {
        var layout = LinuxLayoutDetector.TryResolveKdeLayout(
            () => 1,
            () =>
            [
                ("us", string.Empty, "English (US)"),
                ("de", "nodeadkeys", "German")
            ]);

        Assert.Equal("de", layout);
    }

    [Fact]
    public void TryResolveKdeLayout_ReturnsNull_ForOutOfRangeIndex()
    {
        var layout = LinuxLayoutDetector.TryResolveKdeLayout(
            () => 2,
            () =>
            [
                ("us", string.Empty, "English (US)"),
                ("de", "nodeadkeys", "German")
            ]);

        Assert.Null(layout);
    }

    [Fact]
    public void TryResolveKdeLayout_ReturnsNull_ForEmptyLayouts()
    {
        var layout = LinuxLayoutDetector.TryResolveKdeLayout(
            () => 0,
            () => []);

        Assert.Null(layout);
    }

    [Fact]
    public void TryResolveKdeLayout_ReturnsNull_WhenKdePathThrows()
    {
        var layout = LinuxLayoutDetector.TryResolveKdeLayout(
            () => throw new InvalidOperationException("KDE DBus unavailable"),
            () =>
            [
                ("us", string.Empty, "English (US)")
            ]);

        Assert.Null(layout);
    }
}
