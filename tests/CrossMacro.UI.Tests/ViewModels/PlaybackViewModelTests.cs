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
    private readonly PlaybackViewModel _viewModel;

    public PlaybackViewModelTests()
    {
        _player = Substitute.For<IMacroPlayer>();
        _settingsService = Substitute.For<ISettingsService>();

        _settingsService.Current.Returns(new AppSettings
        {
            PlaybackSpeed = 1.0,
            IsLooping = false,
            LoopCount = 1,
            LoopDelayMs = 0,
            CountdownSeconds = 0
        });

        _viewModel = new PlaybackViewModel(
            _player,
            _settingsService);
    }

    [Fact]
    public void Constructor_InitializesPropertiesFromSettings()
    {
        Assert.Equal(1.0, _viewModel.PlaybackSpeed);
        Assert.False(_viewModel.IsLooping);
        Assert.Equal(1, _viewModel.LoopCount);
        Assert.Equal(0, _viewModel.LoopDelayMs);
    }

    [Fact]
    public async Task PlayMacroAsync_WhenCanPlay_StartsPlayback()
    {
        // Arrange
        var macro = new MacroSequence 
        { 
            Events = { new MacroEvent() } 
        };
        _viewModel.SetMacro(macro);
        _viewModel.CanPlayMacroExternal = true;

        // Act
        await _viewModel.PlayMacroAsync();

        // Assert
        await _player.Received(1).PlayAsync(macro, Arg.Any<PlaybackOptions>());
    }

    [Fact]
    public async Task PlayMacroAsync_WhenCannotPlayExternal_DoesNotStart()
    {
        // Arrange
        var macro = new MacroSequence 
        { 
            Events = { new MacroEvent() } 
        };
        _viewModel.SetMacro(macro);
        _viewModel.CanPlayMacroExternal = false;

        // Act
        await _viewModel.PlayMacroAsync();

        // Assert
        await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>());
    }
    
    [Fact]
    public void TogglePause_WhenPlaying_PausesOrResumes()
    {
        // Arrange
        // Verify TogglePause logic.
        // Needs IsPlaying to be true.
        // We can set IsPlaying by starting playback, but PlayAsync awaits until completion.
        // Using reflection to set IsPlaying for testing Pause logic.
        
        var isPlayingProp = _viewModel.GetType().GetProperty("IsPlaying");
        isPlayingProp?.SetValue(_viewModel, true);
        
        // Test Pause
        _player.IsPaused.Returns(false);
        
        // Act
        _viewModel.TogglePause();
        
        // Assert
        _player.Received(1).Pause();
        Assert.True(_viewModel.IsPaused);
        Assert.Equal("Paused", _viewModel.PlaybackStatus);
        
        // Test Resume
        _player.IsPaused.Returns(true);
        
        // Act
        _viewModel.TogglePause();
        
        // Assert
        _player.Received(1).Resume();
        Assert.False(_viewModel.IsPaused);
    }
}
