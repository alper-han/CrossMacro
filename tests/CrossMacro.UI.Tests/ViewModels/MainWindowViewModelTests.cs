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
    private readonly ISchedulerService _schedulerService;
    private readonly IShortcutService _shortcutService;

    private readonly RecordingViewModel _recordingViewModel;
    private readonly PlaybackViewModel _playbackViewModel;
    private readonly FilesViewModel _filesViewModel;
    private readonly TextExpansionViewModel _textExpansionViewModel;
    private readonly ScheduleViewModel _scheduleViewModel;
    private readonly ShortcutViewModel _shortcutViewModel;
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

        // Fix: TextExpansionViewModel takes (ITextExpansionStorageService, IDialogService, IEnvironmentInfoProvider)
        var textExpansionStorage = Substitute.For<ITextExpansionStorageService>();
        var dialogService = Substitute.For<IDialogService>();
        var environmentInfo = Substitute.For<IEnvironmentInfoProvider>();
        environmentInfo.WindowManagerHandlesCloseButton.Returns(false);
        environmentInfo.CurrentEnvironment.Returns(DisplayEnvironment.Windows);

        _textExpansionViewModel = new TextExpansionViewModel(textExpansionStorage, dialogService, environmentInfo);

        // ScheduleViewModel
        _schedulerService = Substitute.For<ISchedulerService>();
        _schedulerService.LoadAsync().Returns(System.Threading.Tasks.Task.CompletedTask);
        _scheduleViewModel = new ScheduleViewModel(_schedulerService, dialogService);

        // ShortcutViewModel
        _shortcutService = Substitute.For<IShortcutService>();
        _shortcutViewModel = new ShortcutViewModel(_shortcutService, dialogService);

        // Fix: SettingsViewModel takes (IGlobalHotkeyService, ISettingsService, ITextExpansionService, HotkeySettings)
        var hotkeySettings = new HotkeySettings();
        var textExpansionService = Substitute.For<ITextExpansionService>();
        _settingsViewModel = new SettingsViewModel(_hotkeyService, _settingsService, textExpansionService, hotkeySettings);

        // EditorViewModel
        var editorConverter = Substitute.For<IEditorActionConverter>();
        var editorValidator = Substitute.For<IEditorActionValidator>();
        var captureService = Substitute.For<ICoordinateCaptureService>();
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        var editorViewModel = new EditorViewModel(editorConverter, editorValidator, captureService, _fileManager, dialogService, keyCodeMapper);

        // Create SUT
        _viewModel = new MainWindowViewModel(
            _recordingViewModel,
            _playbackViewModel,
            _filesViewModel,
            _textExpansionViewModel,
            _scheduleViewModel,
            _shortcutViewModel,
            _settingsViewModel,
            editorViewModel,
            _hotkeyService,
            _positionProvider,
            environmentInfo,
            null);
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

    [Fact]
    public void DismissUpdateNotification_HidesNotification()
    {
        // Arrange
        _viewModel.IsUpdateNotificationVisible = true;

        // Act
        _viewModel.DismissUpdateNotification();

        // Assert
        Assert.False(_viewModel.IsUpdateNotificationVisible);
    }

    [Fact]
    public void OpenUpdateUrl_AlwaysDismissesNotification()
    {
        // Arrange
        _viewModel.IsUpdateNotificationVisible = true;

        // Act
        _viewModel.OpenUpdateUrl();

        // Assert
        Assert.False(_viewModel.IsUpdateNotificationVisible);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Act
        var act = () =>
        {
            _viewModel.Dispose();
            _viewModel.Dispose();
        };

        // Assert
        Assert.Null(Record.Exception(act));
    }
}
