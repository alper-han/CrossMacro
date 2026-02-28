using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using NSubstitute;
using Xunit;

namespace CrossMacro.Cli.Tests;

public class SettingsCliServiceTests
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
            EnableTextExpansion = false
        };
        _settingsService.Current.Returns(_current);
        _settingsService.Load().Returns(_current);

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
}
