using System;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace CrossMacro.UI.Tests.ViewModels;

public class MainWindowViewModelTests
{
    private readonly IMacroRecorder _recorder;
    private readonly IMacroPlayer _player;
    private readonly IMacroFileManager _fileManager;
    private readonly ISettingsService _settingsService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IMousePositionProvider _positionProvider;
    private readonly IDialogService _filesDialogService;

    private readonly RecordingViewModel _recordingViewModel;
    private readonly PlaybackViewModel _playbackViewModel;
    private readonly FilesViewModel _filesViewModel;
    private readonly TextExpansionViewModel _textExpansionViewModel;
    private readonly SettingsViewModel _settingsViewModel;

    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        // Setup shared mocks
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(new AppSettings());
        
        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
        _positionProvider = Substitute.For<IMousePositionProvider>();

        // Setup child VM dependencies
        _recorder = Substitute.For<IMacroRecorder>();
        _recordingViewModel = new RecordingViewModel(_recorder, _hotkeyService, _settingsService);

        _player = Substitute.For<IMacroPlayer>();
        _playbackViewModel = new PlaybackViewModel(_player, _settingsService);

        _fileManager = Substitute.For<IMacroFileManager>();
        _filesDialogService = Substitute.For<IDialogService>();
        _filesViewModel = new FilesViewModel(_fileManager, _filesDialogService);

        // Fix: TextExpansionViewModel takes (ITextExpansionStorageService, IDialogService)
        var textExpansionStorage = Substitute.For<ITextExpansionStorageService>();
        var dialogService = Substitute.For<IDialogService>();
        _textExpansionViewModel = new TextExpansionViewModel(textExpansionStorage, dialogService);

        // Fix: SettingsViewModel takes (IGlobalHotkeyService, ISettingsService, HotkeySettings)
        var hotkeySettings = new HotkeySettings();
        _settingsViewModel = new SettingsViewModel(_hotkeyService, _settingsService, hotkeySettings);

        // Create SUT
        _viewModel = new MainWindowViewModel(
            _recordingViewModel,
            _playbackViewModel,
            _filesViewModel,
            _textExpansionViewModel,
            _settingsViewModel,
            _hotkeyService,
            _positionProvider);
    }

    [Fact]
    public void Construction_InitializedChildViewModels()
    {
        Assert.NotNull(_viewModel.Recording);
        Assert.NotNull(_viewModel.Playback);
        Assert.NotNull(_viewModel.Files);
        Assert.NotNull(_viewModel.TextExpansion);
        Assert.NotNull(_viewModel.Settings);
    }

    [Fact]
    public void RecordingStateChanged_UpdatesPlaybackAvailability()
    {
        // Arrange
        var recordingProp = _recordingViewModel.GetType().GetProperty("IsRecording");
        
        // Act - Start recording
        recordingProp?.SetValue(_recordingViewModel, true);

        // Assert
        Assert.False(_playbackViewModel.CanPlayMacroExternal);

        // Act - Stop recording
        recordingProp?.SetValue(_recordingViewModel, false);

        // Assert
        Assert.True(_playbackViewModel.CanPlayMacroExternal);
    }

    [Fact]
    public void PlaybackStateChanged_UpdatesRecordingAvailability()
    {
        // Arrange
        var playbackProp = _playbackViewModel.GetType().GetProperty("IsPlaying");
        
        // Act
        playbackProp?.SetValue(_playbackViewModel, true);

        // Assert
         Assert.False(_recordingViewModel.CanStartRecordingExternal);

        playbackProp?.SetValue(_playbackViewModel, false);

        // Assert
        Assert.True(_recordingViewModel.CanStartRecordingExternal);
    }

    [Fact]
    public async System.Threading.Tasks.Task MacroLoaded_UpdatesRecordingAndPlayback()
    {
        // Arrange
        var macro = new MacroSequence 
        { 
            Name = "TestMacro",
            Events = { new MacroEvent { Type = EventType.MouseMove } }
        };

        // Setup mocks to simulate successful load
        _filesDialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/path/to/macro.macro"));
        
        _fileManager.LoadAsync("/path/to/macro.macro")
            .Returns(Task.FromResult<MacroSequence?>(macro));

        // Act
        await _filesViewModel.LoadMacroAsync();

        // Assert
        Assert.Equal("Loaded: TestMacro", _viewModel.GlobalStatus);
    }
}
