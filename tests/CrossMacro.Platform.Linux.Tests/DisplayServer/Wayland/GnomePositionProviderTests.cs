namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;


public sealed class GnomePositionProviderTests
{
    [Fact]
    public void Constructor_OnGnomeX11_DoesNotEnableShellExtensionProvider()
    {
        var environment = default(LinuxEnvironmentSnapshot) with
        {
            SessionType = "x11",
            Display = ":0",
            CurrentDesktop = "GNOME",
            GdmSession = "gnome-xorg",
        };

        using var provider = new GnomePositionProvider(environment);

        Assert.False(provider.IsSupported);
    }

    [Fact]
    public void EmbeddedExtension_ShouldPublishCapturedPointerMotion()
    {
        const string resourceName = "CrossMacro.Platform.Linux.DisplayServer.Wayland.GnomePositionProvider.js";
        using var stream = typeof(GnomePositionProvider).Assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var script = reader.ReadToEnd();

        Assert.Contains("<signal name=\"PositionChanged\">", script, StringComparison.Ordinal);
        Assert.Contains("global.stage.connect('captured-event'", script, StringComparison.Ordinal);
        Assert.Contains("event.type() === Clutter.EventType.MOTION", script, StringComparison.Ordinal);
        Assert.Contains("this._dbusImpl.emit_signal(", script, StringComparison.Ordinal);
        Assert.Contains("new GLib.Variant('(ii)', [x, y])", script, StringComparison.Ordinal);
        Assert.Contains("this._nameOwnerId = Gio.DBus.session.own_name(", script, StringComparison.Ordinal);
        Assert.Contains("Gio.DBus.session.unown_name(this._nameOwnerId)", script, StringComparison.Ordinal);
        Assert.True(typeof(IMousePositionChangeSource).IsAssignableFrom(typeof(GnomePositionProvider)));
    }

    [Fact]
    public void EmbeddedExtension_ShouldAnswerCaptureAreaFromAnInvocationBasedHandler()
    {
        const string resourceName = "CrossMacro.Platform.Linux.DisplayServer.Wayland.GnomePositionProvider.js";
        using var stream = typeof(GnomePositionProvider).Assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var script = reader.ReadToEnd();

        // GJS prefers a handler named like the method over <name>Async, and only the newest GJS
        // understands a promise-returning one, so CaptureArea must be answered through the invocation.
        Assert.Contains("CaptureAreaAsync(parameters, invocation)", script, StringComparison.Ordinal);
        Assert.Contains("invocation.return_value(new GLib.Variant('(sib)'", script, StringComparison.Ordinal);
        Assert.Contains("invocation.return_dbus_error(", script, StringComparison.Ordinal);
        Assert.DoesNotContain("    CaptureArea(", script, StringComparison.Ordinal);
        Assert.DoesNotContain("async CaptureArea(", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadPositionChangedSignal_ReturnsCoordinates()
    {
        var body = DbusWrapperProtocolTestHelpers.EncodeInt32Body(-1920)
            .Concat(DbusWrapperProtocolTestHelpers.EncodeInt32Body(540))
            .ToArray();
        var message = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(body);

        var position = GnomeTrackerClient.ReadPositionChangedSignal(message, state: null);

        Assert.Equal((-1920, 540), position);
    }

    [Fact]
    public void TryReadEnabledState_ReturnsTrue_ForActiveExtensionInfo()
    {
        var info = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["state"] = (uint)1,
            ["name"] = "Cursor Spy",
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

        var info = GnomeShellExtensionsClient.ReadGetExtensionInfoReply(reply, _: null);

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
            ["state"] = state,
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
                ["state"] = 1d,
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
            resolutionUnavailableLogged: false);

        Assert.Null(result.Resolution);
        Assert.True(result.ResolutionUnavailableLogged);
    }

    [Fact]
    public async Task TryGetScreenResolutionAsync_ReportsTheCurrentLayoutOnEveryCall()
    {
        var answers = new Queue<(int width, int height)>([(1920, 1080), (3840, 1080)]);

        var first = await GnomePositionProvider.TryGetScreenResolutionAsync(
            () => Task.FromResult(answers.Dequeue()),
            resolutionUnavailableLogged: false);
        var afterMonitorWasAdded = await GnomePositionProvider.TryGetScreenResolutionAsync(
            () => Task.FromResult(answers.Dequeue()),
            resolutionUnavailableLogged: false);

        Assert.Equal((1920, 1080), first.Resolution);
        Assert.Equal((3840, 1080), afterMonitorWasAdded.Resolution);
        Assert.False(afterMonitorWasAdded.ResolutionUnavailableLogged);
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(-1, 1080)]
    [InlineData(1920, -1)]
    public async Task TryGetScreenResolutionAsync_ReturnsNullForNonPositiveDimensions(int width, int height)
    {
        var result = await GnomePositionProvider.TryGetScreenResolutionAsync(
            () => Task.FromResult((width, height)),
            resolutionUnavailableLogged: false);

        Assert.Null(result.Resolution);
        Assert.True(result.ResolutionUnavailableLogged);
    }

    [Fact]
    public async Task EnsureFileContentAsync_ShouldCreateFile_WhenMissing()
    {
        using var tempDir = new TempDirectory();
        var filePath = Path.Combine(tempDir.Path, "extension.js");
        const string expectedContent = "new-content";

        var changed = await GnomePositionProvider.EnsureFileContentAsync(filePath, expectedContent, CancellationToken.None);

        Assert.True(changed);
        Assert.True(File.Exists(filePath));
        Assert.Equal(expectedContent, await File.ReadAllTextAsync(filePath, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureFileContentAsync_ShouldNotRewrite_WhenContentMatches()
    {
        using var tempDir = new TempDirectory();
        var filePath = Path.Combine(tempDir.Path, "extension.js");
        const string expectedContent = "same-content";
        await File.WriteAllTextAsync(filePath, expectedContent, CancellationToken.None);

        var changed = await GnomePositionProvider.EnsureFileContentAsync(filePath, expectedContent, CancellationToken.None);

        Assert.False(changed);
        Assert.Equal(expectedContent, await File.ReadAllTextAsync(filePath, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureFileContentAsync_ShouldRewrite_WhenContentDiffers()
    {
        using var tempDir = new TempDirectory();
        var filePath = Path.Combine(tempDir.Path, "extension.js");
        await File.WriteAllTextAsync(filePath, "old-content", CancellationToken.None);
        const string expectedContent = "updated-content";

        var changed = await GnomePositionProvider.EnsureFileContentAsync(filePath, expectedContent, CancellationToken.None);

        Assert.True(changed);
        Assert.Equal(expectedContent, await File.ReadAllTextAsync(filePath, CancellationToken.None));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crossmacro-tests-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            _ = Directory.CreateDirectory(Path);
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
