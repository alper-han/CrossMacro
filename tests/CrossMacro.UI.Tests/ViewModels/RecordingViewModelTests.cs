using System;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace CrossMacro.UI.Tests.ViewModels;

public class RecordingViewModelTests
{
    private readonly IMacroRecorder _recorder;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsService _settingsService;
    private readonly RecordingViewModel _viewModel;

    public RecordingViewModelTests()
    {
        _recorder = Substitute.For<IMacroRecorder>();
        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
        _settingsService = Substitute.For<ISettingsService>();

        // Setup default settings
        _settingsService.Current.Returns(new AppSettings 
        { 
            IsMouseRecordingEnabled = true, 
            IsKeyboardRecordingEnabled = true 
        });

        _viewModel = new RecordingViewModel(
            _recorder,
            _hotkeyService,
            _settingsService);
    }

    [Fact]
    public void Constructor_InitializesPropertiesFromSettings()
    {
        // Assert
        Assert.True(_viewModel.IsMouseRecordingEnabled);
        Assert.True(_viewModel.IsKeyboardRecordingEnabled);
        Assert.False(_viewModel.IsRecording);
        Assert.Equal("Ready", _viewModel.RecordingStatus);
    }

    [Fact]
    public async Task StartRecordingAsync_WhenCanStart_StartsRecording()
    {
        // Arrange
        _viewModel.CanStartRecordingExternal = true;

        // Act
        await _viewModel.StartRecordingAsync();

        // Assert
        Assert.True(_viewModel.IsRecording);
        Assert.Equal("Recording...", _viewModel.RecordingStatus);
        await _recorder.Received(1).StartRecordingAsync(
            Arg.Any<bool>(), 
            Arg.Any<bool>(), 
            Arg.Any<int[]>());
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(false);
    }

    [Fact]
    public async Task StartRecordingAsync_WhenCannotStartExternal_DoesNotStart()
    {
        // Arrange
        _viewModel.CanStartRecordingExternal = false;

        // Act
        await _viewModel.StartRecordingAsync();

        // Assert
        Assert.False(_viewModel.IsRecording);
        await _recorder.DidNotReceive().StartRecordingAsync(
            Arg.Any<bool>(), 
            Arg.Any<bool>(), 
            Arg.Any<int[]>());
    }

    [Fact]
    public void StopRecording_WhenRecording_StopsAndReturnsMacro()
    {
        // Arrange
        var expectedMacro = new MacroSequence();
        expectedMacro.Events.Add(new MacroEvent { Type = EventType.MouseMove });
        _recorder.StopRecording().Returns(expectedMacro);

        // Manually set IsRecording to true via reflection or by calling StartRecordingAsync
        _viewModel.GetType().GetProperty("IsRecording")?.SetValue(_viewModel, true);

        // Act
        var result = _viewModel.StopRecording();

        // Assert
        Assert.False(_viewModel.IsRecording); // Should be false after stop
        Assert.Equal(expectedMacro, result);
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(true);
    }

    [Fact]
    public void StopRecording_WhenNotRecording_ReturnsNull()
    {
        // Arrange
        _viewModel.GetType().GetProperty("IsRecording")?.SetValue(_viewModel, false);

        // Act
        var result = _viewModel.StopRecording();

        // Assert
        Assert.Null(result);
        _recorder.DidNotReceive().StopRecording();
    }

    [Fact]
    public void ToggleRecording_WhenRecording_Stops()
    {
        // Arrange
        _viewModel.GetType().GetProperty("IsRecording")?.SetValue(_viewModel, true);
        _recorder.StopRecording().Returns(new MacroSequence());

        // Act
        _viewModel.ToggleRecording();

        // Assert
        Assert.False(_viewModel.IsRecording);
        _recorder.Received(1).StopRecording();
    }

    [Fact]
    public void ToggleRecording_WhenNotRecording_Starts()
    {
        // Arrange
        _viewModel.CanStartRecordingExternal = true;
        _viewModel.GetType().GetProperty("IsRecording")?.SetValue(_viewModel, false);

        // Act
        _viewModel.ToggleRecording();

        // Assert
        Assert.True(_viewModel.IsRecording);
    }
}
