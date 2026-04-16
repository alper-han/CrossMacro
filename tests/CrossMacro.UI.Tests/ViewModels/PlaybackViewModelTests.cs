using System;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace CrossMacro.UI.Tests.ViewModels;

public class PlaybackViewModelTests
{
    private readonly IMacroPlayer _player;
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly PlaybackViewModel _viewModel;

    public PlaybackViewModelTests()
    {
        _player = Substitute.For<IMacroPlayer>();
        _settingsService = Substitute.For<ISettingsService>();
        _settings = new AppSettings
        {
            PlaybackSpeed = 1.0,
            IsLooping = false,
            LoopCount = 1,
            LoopDelayMs = 0,
            UseRandomLoopDelay = false,
            LoopDelayMinMs = 0,
            LoopDelayMaxMs = 0,
            CountdownSeconds = 0
        };

        _settingsService.Current.Returns(_settings);
        _viewModel = new PlaybackViewModel(_player, _settingsService);
    }

    [Fact]
    public void Constructor_InitializesPropertiesFromSettings()
    {
        Assert.Equal(1.0, _viewModel.PlaybackSpeed);
        Assert.False(_viewModel.IsLooping);
        Assert.Equal(1, _viewModel.LoopCount);
        Assert.Equal(0, _viewModel.LoopDelayMs);
        Assert.False(_viewModel.UseRandomLoopDelay);
        Assert.Equal(0, _viewModel.LoopDelayMinMs);
        Assert.Equal(0, _viewModel.LoopDelayMaxMs);
    }

    [Fact]
    public void RandomLoopDelay_TogglesVisibleInputs()
    {
        _viewModel.IsLooping = true;

        Assert.True(_viewModel.ShowFixedLoopDelayInput);
        Assert.False(_viewModel.ShowRandomLoopDelayInputs);

        _viewModel.UseRandomLoopDelay = true;

        Assert.False(_viewModel.ShowFixedLoopDelayInput);
        Assert.True(_viewModel.ShowRandomLoopDelayInputs);
    }

    [Fact]
    public void RandomLoopDelay_MaxClampsToMin()
    {
        _viewModel.UseRandomLoopDelay = true;
        _viewModel.LoopDelayMinMs = 300;
        _viewModel.LoopDelayMaxMs = 100;

        Assert.Equal(300, _viewModel.LoopDelayMinMs);
        Assert.Equal(300, _viewModel.LoopDelayMaxMs);
    }

    [Fact]
    public async Task PlayMacroAsync_WhenCanPlay_StartsPlayback()
    {
        var macro = new MacroSequence
        {
            Events = { new MacroEvent() }
        };
        _viewModel.SetMacro(macro);
        _viewModel.CanPlayMacroExternal = true;

        await _viewModel.PlayMacroAsync();

        await _player.Received(1).PlayAsync(macro, Arg.Any<PlaybackOptions>());
    }

    [Fact]
    public async Task PlayMacroAsync_WhenRandomLoopDelayEnabled_ForwardsRandomDelayOptions()
    {
        var macro = new MacroSequence
        {
            Events = { new MacroEvent() }
        };
        PlaybackOptions? capturedOptions = null;
        _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>())
            .Returns(ci =>
            {
                capturedOptions = ci.ArgAt<PlaybackOptions>(1);
                return Task.CompletedTask;
            });

        _viewModel.SetMacro(macro);
        _viewModel.IsLooping = true;
        _viewModel.LoopCount = 3;
        _viewModel.LoopDelayMs = 90;
        _viewModel.UseRandomLoopDelay = true;
        _viewModel.LoopDelayMinMs = 120;
        _viewModel.LoopDelayMaxMs = 240;

        await _viewModel.PlayMacroAsync();

        Assert.NotNull(capturedOptions);
        Assert.True(capturedOptions!.Loop);
        Assert.Equal(3, capturedOptions.RepeatCount);
        Assert.Equal(90, capturedOptions.RepeatDelayMs);
        Assert.True(capturedOptions.UseRandomRepeatDelay);
        Assert.Equal(120, capturedOptions.RepeatDelayMinMs);
        Assert.Equal(240, capturedOptions.RepeatDelayMaxMs);
    }

    [Fact]
    public async Task PlayMacroAsync_WhenCannotPlayExternal_DoesNotStart()
    {
        var macro = new MacroSequence
        {
            Events = { new MacroEvent() }
        };
        _viewModel.SetMacro(macro);
        _viewModel.CanPlayMacroExternal = false;

        await _viewModel.PlayMacroAsync();

        await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>());
    }

    [Fact]
    public void TogglePause_WhenPlaying_PausesOrResumes()
    {
        var isPlayingProp = _viewModel.GetType().GetProperty("IsPlaying");
        isPlayingProp?.SetValue(_viewModel, true);

        _player.IsPaused.Returns(false);

        _viewModel.TogglePause();

        _player.Received(1).Pause();
        Assert.True(_viewModel.IsPaused);
        Assert.Equal("Paused", _viewModel.PlaybackStatus);

        _player.IsPaused.Returns(true);

        _viewModel.TogglePause();

        _player.Received(1).Resume();
        Assert.False(_viewModel.IsPaused);
    }

    [Fact]
    public void StopPlayback_WhenPlaying_StopsPlayerAndSetsStatus()
    {
        _viewModel.GetType().GetProperty("IsPlaying")?.SetValue(_viewModel, true);

        _viewModel.StopPlayback();

        _player.Received(1).Stop();
        Assert.False(_viewModel.IsPlaying);
        Assert.Equal("Playback stopped", _viewModel.PlaybackStatus);
    }

    [Fact]
    public async Task PlayMacroAsync_WhenPlayerThrows_SetsErrorStatus_AndResetsPlaying()
    {
        var macro = new MacroSequence { Events = { new MacroEvent() } };
        _viewModel.SetMacro(macro);
        _viewModel.CanPlayMacroExternal = true;
        _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>())
            .Returns(Task.FromException(new InvalidOperationException("simulator failed")));

        await _viewModel.PlayMacroAsync();

        Assert.False(_viewModel.IsPlaying);
        Assert.Contains("Playback error", _viewModel.PlaybackStatus, StringComparison.Ordinal);
        Assert.Contains("simulator failed", _viewModel.PlaybackStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void TogglePlayback_WhenCannotPlay_DoesNotInvokePlayer()
    {
        _viewModel.CanPlayMacroExternal = false;

        _viewModel.TogglePlayback();

        _player.DidNotReceive().Stop();
        _ = _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>());
    }

    [Fact]
    public void PlaybackSpeed_WhenSaveFails_RollsBackValue()
    {
        _settingsService.When(x => x.Save()).Do(_ => throw new InvalidOperationException("disk full"));

        _viewModel.PlaybackSpeed = 2.0;

        Assert.Equal(1.0, _viewModel.PlaybackSpeed);
        Assert.Equal(1.0, _settings.PlaybackSpeed);
    }
}
