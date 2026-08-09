
namespace CrossMacro.Cli.Tests;

public sealed class SettingsCliServiceTests
{
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _current;
    private readonly ISettingsCliService _service;

    public SettingsCliServiceTests()
    {
        _settingsService = Substitute.For<ISettingsService>();
        _current = new AppSettings
        {
            PlaybackSpeed = 1.0,
            IsLooping = false,
            LoopCount = 1,
            LoopDelayMs = 0,
            CountdownSeconds = 0,
            LogLevel = "Information",
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true,
            ForceRelativeCoordinates = false,
            SkipInitialZeroZero = false,
            EnableTextExpansion = false,
            Theme = "Mocha",
            Language = "en",
            EnableTrayIcon = false,
            StartMinimized = false,
            CheckForUpdates = false,
            PortalScreenCastRestoreToken = "secret-token",
        };
        _ = _settingsService.Current.Returns(_current);
        _ = _settingsService.LoadAsync().Returns(Task.FromResult(_current));

        _service = new SettingsCliService(_settingsService);
    }

    [Fact]
    public async Task GetAsync_WithKnownKey_ReturnsValue()
    {
        var result = await _service.GetAsync("playback.speed", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CliExitCode.Success, result.ExitCode);
    }

    [Fact]
    public async Task GetAsync_WithUnknownKey_ReturnsInvalidArguments()
    {
        var result = await _service.GetAsync("unknown.key", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.InvalidArguments, result.ExitCode);
    }

    [Fact]
    public async Task SetAsync_WithValidValue_UpdatesAndSaves()
    {
        var result = await _service.SetAsync("playback.loop", "true", CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(_current.IsLooping);
        await _settingsService.Received(1).SaveAsync();
    }

    [Fact]
    public async Task SetAsync_WithInvalidValue_ReturnsInvalidArguments()
    {
        var result = await _service.SetAsync("playback.loopCount", "-1", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.InvalidArguments, result.ExitCode);
    }

    [Fact]
    public async Task SetAsync_WithRecordingMouseKey_UpdatesAndSaves()
    {
        var result = await _service.SetAsync("recording.mouse", "false", CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(_current.IsMouseRecordingEnabled);
        await _settingsService.Received(1).SaveAsync();
    }

    [Fact]
    public async Task SetAsync_WithRecordingKeyboardKey_UpdatesAndSaves()
    {
        var result = await _service.SetAsync("recording.keyboard", "false", CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(_current.IsKeyboardRecordingEnabled);
        await _settingsService.Received(1).SaveAsync();
    }

    [Fact]
    public async Task GetAsync_WithPortalRestoreToken_ReturnsStatusNotRawToken()
    {
        var result = await _service.GetAsync("screen.portalRestoreToken", CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<SettingsValueData>(result.Data);
        Assert.Equal("set", data.Value);
    }

    [Fact]
    public async Task SetAsync_WithUiTheme_UpdatesAndSaves()
    {
        var result = await _service.SetAsync("ui.theme", "Nord", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Nord", _current.Theme);
        await _settingsService.Received(1).SaveAsync();
    }

    [Fact]
    public async Task SetAsync_WithPortalRestoreToken_ReturnsInvalidArguments()
    {
        var result = await _service.SetAsync("screen.portalRestoreToken", "raw", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.InvalidArguments, result.ExitCode);
        await _settingsService.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ResetAsync_WithPortalRestoreToken_ClearsToken()
    {
        var result = await _service.ResetAsync("screen.portalRestoreToken", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(_current.PortalScreenCastRestoreToken);
        await _settingsService.Received(1).SaveAsync();
    }

    [Fact]
    public async Task ListKeysAsync_ReturnsExpandedKeys()
    {
        var result = await _service.ListKeysAsync(CancellationToken.None);

        Assert.True(result.Success);
        var keys = Assert.IsType<List<string>>(result.Data);
        Assert.Contains("ui.theme", keys);
        Assert.Contains("updates.checkForUpdates", keys);
    }

    [Fact]
    public async Task MaximumMotionErrorPixels_CanBeGetSetAndReset()
    {
        _current.MaximumMotionErrorPixels = 4d;

        var setResult = await _service.SetAsync("playback.maximumMotionErrorPixels", "1.25", CancellationToken.None);
        var getResult = await _service.GetAsync("playback.maximumMotionErrorPixels", CancellationToken.None);
        var resetResult = await _service.ResetAsync("playback.maximumMotionErrorPixels", CancellationToken.None);

        Assert.True(setResult.Success);
        Assert.True(getResult.Success);
        Assert.Equal(1.25d, Assert.IsType<double>(Assert.IsType<SettingsValueData>(getResult.Data).Value));
        Assert.True(resetResult.Success);
        Assert.Equal(PlaybackOptions.DefaultMaximumMotionErrorPixels, _current.MaximumMotionErrorPixels);
        await _settingsService.Received(2).SaveAsync();
    }
}
