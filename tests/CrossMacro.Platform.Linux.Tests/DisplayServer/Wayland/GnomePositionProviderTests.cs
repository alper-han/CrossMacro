namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;
using CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;

public class GnomePositionProviderTests
{
    [Fact]
    public void TryReadEnabledState_ReturnsTrue_ForActiveExtensionInfo()
    {
        var info = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["state"] = (uint)1,
            ["name"] = "Cursor Spy"
        };

        var enabled = GnomePositionProvider.TryReadEnabledState(info);

        Assert.True(enabled);
    }

    [Fact]
    public void TryReadEnabledState_ReturnsTrue_ForParsedExtensionInfoReply()
    {
        var reply = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(
            DbusWrapperProtocolTestHelpers.EncodeStringVariantDictionaryBody(
                ("state", (object)(uint)1),
                ("name", "Cursor Spy"),
                ("enabled", true)));

        var info = GnomeShellExtensionsClient.ReadGetExtensionInfoReply(reply, null);

        var enabled = GnomePositionProvider.TryReadEnabledState(info);

        Assert.True(enabled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void TryReadEnabledState_ReturnsFalse_ForInactiveStates(int state)
    {
        var info = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["state"] = state
        };

        var enabled = GnomePositionProvider.TryReadEnabledState(info);

        Assert.False(enabled);
    }

    [Fact]
    public async Task IsExtensionEnabledAsync_ReturnsTrue_WhenInfoParserFindsEnabledState()
    {
        var enabled = await GnomePositionProvider.IsExtensionEnabledAsync(
            () => Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["state"] = 1d
            }));

        Assert.True(enabled);
    }

    [Fact]
    public async Task TryGetAbsolutePositionAsync_ReturnsPosition_WhenTrackerQuerySucceeds()
    {
        var position = await GnomePositionProvider.TryGetAbsolutePositionAsync(
            () => Task.FromResult((x: 640, y: 480)));

        Assert.Equal((640, 480), position);
    }

    [Fact]
    public async Task TryGetScreenResolutionAsync_ReturnsNull_WhenServiceIsUnavailable()
    {
        var result = await GnomePositionProvider.TryGetScreenResolutionAsync(
            () => Task.FromException<(int width, int height)>(
                new InvalidOperationException("org.freedesktop.DBus.Error.ServiceUnknown: name is not activatable")),
            cachedResolution: null,
            resolutionUnavailableLogged: false);

        Assert.Null(result.Resolution);
        Assert.Null(result.CachedResolution);
        Assert.True(result.ResolutionUnavailableLogged);
    }

    [Fact]
    public async Task TryGetScreenResolutionAsync_ReturnsCachedResolution_WithoutCallingTracker()
    {
        var calls = 0;
        var cached = (Width: 1920, Height: 1080);

        var result = await GnomePositionProvider.TryGetScreenResolutionAsync(
            () =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult((width: 3840, height: 2160));
            },
            cached,
            resolutionUnavailableLogged: false);

        Assert.Equal(cached, result.Resolution);
        Assert.Equal(cached, result.CachedResolution);
        Assert.False(result.ResolutionUnavailableLogged);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task TryGetScreenResolutionAsync_CachesSuccessfulTrackerResolution()
    {
        var result = await GnomePositionProvider.TryGetScreenResolutionAsync(
            () => Task.FromResult((width: 2560, height: 1440)),
            cachedResolution: null,
            resolutionUnavailableLogged: false);

        Assert.Equal((2560, 1440), result.Resolution);
        Assert.Equal((2560, 1440), result.CachedResolution);
        Assert.False(result.ResolutionUnavailableLogged);
    }

    [Fact]
    public async Task EnsureFileContentAsync_ShouldCreateFile_WhenMissing()
    {
        using var tempDir = new TempDirectory();
        var filePath = Path.Combine(tempDir.Path, "extension.js");
        const string expectedContent = "new-content";

        var changed = await GnomePositionProvider.EnsureFileContentAsync(filePath, expectedContent);

        Assert.True(changed);
        Assert.True(File.Exists(filePath));
        Assert.Equal(expectedContent, await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task EnsureFileContentAsync_ShouldNotRewrite_WhenContentMatches()
    {
        using var tempDir = new TempDirectory();
        var filePath = Path.Combine(tempDir.Path, "extension.js");
        const string expectedContent = "same-content";
        await File.WriteAllTextAsync(filePath, expectedContent);

        var changed = await GnomePositionProvider.EnsureFileContentAsync(filePath, expectedContent);

        Assert.False(changed);
        Assert.Equal(expectedContent, await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task EnsureFileContentAsync_ShouldRewrite_WhenContentDiffers()
    {
        using var tempDir = new TempDirectory();
        var filePath = Path.Combine(tempDir.Path, "extension.js");
        await File.WriteAllTextAsync(filePath, "old-content");
        const string expectedContent = "updated-content";

        var changed = await GnomePositionProvider.EnsureFileContentAsync(filePath, expectedContent);

        Assert.True(changed);
        Assert.Equal(expectedContent, await File.ReadAllTextAsync(filePath));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crossmacro-tests-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
