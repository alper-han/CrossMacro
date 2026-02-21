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
    public void StopRecording_WhenRecordingCompletedHandlerThrows_DoesNotConvertSuccessToError()
    {
        // Arrange
        var expectedMacro = new MacroSequence();
        expectedMacro.Events.Add(new MacroEvent { Type = EventType.MouseMove });
        _recorder.StopRecording().Returns(expectedMacro);
        _viewModel.RecordingCompleted += (_, _) => throw new NullReferenceException("handler failure");
        _viewModel.GetType().GetProperty("IsRecording")?.SetValue(_viewModel, true);

        // Act
        var result = _viewModel.StopRecording();

        // Assert
        Assert.Equal(expectedMacro, result);
        Assert.Equal("Recorded 1 events", _viewModel.RecordingStatus);
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(true);
    }

    [Fact]
    public void StopRecording_WhenMacroEventsCollectionIsNull_DoesNotThrowOrSetErrorStatus()
    {
        // Arrange
        _recorder.StopRecording().Returns(new MacroSequence { Events = null! });
        _viewModel.GetType().GetProperty("IsRecording")?.SetValue(_viewModel, true);

        // Act
        var result = _viewModel.StopRecording();

        // Assert
        Assert.Null(result);
        Assert.Equal("Ready", _viewModel.RecordingStatus);
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

    [Fact]
    public async Task StartRecordingAsync_WhenRecorderThrows_ReenablesHotkeysAndResetsState()
    {
        // Arrange
        _viewModel.CanStartRecordingExternal = true;
        _recorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("start failed")));

        // Act
        await _viewModel.StartRecordingAsync();

        // Assert
        Assert.False(_viewModel.IsRecording);
        Assert.Equal("Ready", _viewModel.RecordingStatus);
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(false);
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(true);
    }

    [Fact]
    public async Task StartRecordingAsync_UsesForceRelativeAndSkipInitialZeroSettings()
    {
        // Arrange
        _viewModel.CanStartRecordingExternal = true;
        _viewModel.ForceRelativeCoordinates = true;
        _viewModel.SkipInitialZeroZero = true;

        // Act
        await _viewModel.StartRecordingAsync();

        // Assert
        await _recorder.Received(1).StartRecordingAsync(
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IEnumerable<int>>(),
            _viewModel.ForceRelativeCoordinates,
            _viewModel.SkipInitialZeroZero,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void StopRecording_WhenRecorderThrows_ReturnsNullAndResetsState()
    {
        // Arrange
        _recorder.StopRecording().Returns(_ => throw new InvalidOperationException("stop failed"));
        _viewModel.GetType().GetProperty("IsRecording")?.SetValue(_viewModel, true);

        // Act
        var result = _viewModel.StopRecording();

        // Assert
        Assert.Null(result);
        Assert.False(_viewModel.IsRecording);
        Assert.Equal("Ready", _viewModel.RecordingStatus);
        _hotkeyService.Received(1).SetPlaybackPauseHotkeysEnabled(true);
    }

    [Fact]
    public void SetMacro_UpdatesEventCountersByType()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Events =
            {
                new MacroEvent { Type = EventType.MouseMove },
                new MacroEvent { Type = EventType.ButtonPress },
                new MacroEvent { Type = EventType.KeyPress },
                new MacroEvent { Type = EventType.KeyRelease }
            }
        };

        // Act
        _viewModel.SetMacro(macro);

        // Assert
        Assert.Equal(4, _viewModel.EventCount);
        Assert.Equal(2, _viewModel.MouseEventCount);
        Assert.Equal(2, _viewModel.KeyboardEventCount);
        Assert.Equal("Loaded 4 events", _viewModel.RecordingStatus);
    }
}
