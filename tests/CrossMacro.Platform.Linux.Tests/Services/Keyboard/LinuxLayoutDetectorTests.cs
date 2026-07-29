
namespace CrossMacro.Platform.Linux.Tests.Services.Keyboard;

public sealed class LinuxLayoutDetectorTests
{
    [Fact]
    public async Task TryResolveKdeLayout_ReturnsShortName_ForValidIndex()
    {
        var layout = await LinuxLayoutDetector.TryResolveKdeLayoutAsync(
            () => Task.FromResult<uint>(1),
            () => Task.FromResult<(string shortName, string variant, string displayName)[]>(
            [
                ("us", string.Empty, "English (US)"),
                ("de", "nodeadkeys", "German"),
            ]),
            CancellationToken.None);

        Assert.Equal("de", layout);
    }

    [Fact]
    public async Task TryResolveKdeLayout_ReturnsNull_ForOutOfRangeIndex()
    {
        var layout = await LinuxLayoutDetector.TryResolveKdeLayoutAsync(
            () => Task.FromResult<uint>(2),
            () => Task.FromResult<(string shortName, string variant, string displayName)[]>(
            [
                ("us", string.Empty, "English (US)"),
                ("de", "nodeadkeys", "German"),
            ]),
            CancellationToken.None);

        Assert.Null(layout);
    }

    [Fact]
    public async Task TryResolveKdeLayout_ReturnsNull_ForEmptyLayouts()
    {
        var layout = await LinuxLayoutDetector.TryResolveKdeLayoutAsync(
            () => Task.FromResult<uint>(0),
            () => Task.FromResult<(string shortName, string variant, string displayName)[]>([]),
            CancellationToken.None);

        Assert.Null(layout);
    }

    [Fact]
    public async Task TryResolveKdeLayout_ReturnsNull_WhenKdePathThrows()
    {
        var layout = await LinuxLayoutDetector.TryResolveKdeLayoutAsync(
            () => Task.FromException<uint>(new InvalidOperationException("KDE DBus unavailable")),
            () => Task.FromResult<(string shortName, string variant, string displayName)[]>(
            [
                ("us", string.Empty, "English (US)"),
            ]),
            CancellationToken.None);

        Assert.Null(layout);
    }
}
