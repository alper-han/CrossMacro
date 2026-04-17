using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using FluentAssertions;
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
    private readonly IExternalUrlOpener _externalUrlOpener;
    private readonly ISchedulerService _schedulerService;
    private readonly IShortcutService _shortcutService;
    private readonly LoadedMacroSession _loadedMacroSession;

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
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(new AppSettings());

        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
        _positionProvider = Substitute.For<IMousePositionProvider>();
        _loadedMacroSession = new LoadedMacroSession();

        _recorder = Substitute.For<IMacroRecorder>();
        _recordingViewModel = new RecordingViewModel(_recorder, _hotkeyService, _settingsService);

        _player = Substitute.For<IMacroPlayer>();
        _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.CompletedTask);
        _playbackViewModel = new PlaybackViewModel(_player, _settingsService, _loadedMacroSession);

        _fileManager = Substitute.For<IMacroFileManager>();
        _filesDialogService = Substitute.For<IDialogService>();
        _externalUrlOpener = Substitute.For<IExternalUrlOpener>();
        _filesViewModel = new FilesViewModel(_fileManager, _filesDialogService, _loadedMacroSession);

        var textExpansionStorage = Substitute.For<ITextExpansionStorageService>();
        var dialogService = Substitute.For<IDialogService>();
        var environmentInfo = Substitute.For<IEnvironmentInfoProvider>();
        environmentInfo.WindowManagerHandlesCloseButton.Returns(false);
        environmentInfo.CurrentEnvironment.Returns(DisplayEnvironment.Windows);

        _textExpansionViewModel = new TextExpansionViewModel(textExpansionStorage, dialogService, environmentInfo);

        _schedulerService = Substitute.For<ISchedulerService>();
        _schedulerService.LoadAsync().Returns(Task.CompletedTask);
        var timeProvider = Substitute.For<ITimeProvider>();
        timeProvider.Now.Returns(new DateTime(2026, 1, 1, 10, 0, 0));
        timeProvider.UtcNow.Returns(new DateTime(2026, 1, 1, 7, 0, 0));
        _scheduleViewModel = new ScheduleViewModel(_schedulerService, dialogService, timeProvider);

        _shortcutService = Substitute.For<IShortcutService>();
        _shortcutViewModel = new ShortcutViewModel(_shortcutService, dialogService);

        var hotkeySettings = new HotkeySettings();
        var textExpansionService = Substitute.For<ITextExpansionService>();
        var themeService = Substitute.For<IThemeService>();
        themeService.AvailableThemes.Returns(new[] { "Classic" });
        themeService.CurrentTheme.Returns("Classic");
        themeService
            .TryApplyTheme(Arg.Any<string>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });
        _settingsViewModel = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            textExpansionService,
            hotkeySettings,
            _externalUrlOpener,
            themeService);

        var editorConverter = Substitute.For<IEditorActionConverter>();
        var editorValidator = Substitute.For<IEditorActionValidator>();
        var captureService = Substitute.For<ICoordinateCaptureService>();
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        var editorViewModel = new EditorViewModel(editorConverter, editorValidator, captureService, _fileManager, dialogService, keyCodeMapper);

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
            _externalUrlOpener,
            null);
    }

    [Fact]
    public void Construction_InitializedChildViewModels()
    {
        _viewModel.Recording.Should().NotBeNull();
        _viewModel.Playback.Should().NotBeNull();
        _viewModel.Files.Should().NotBeNull();
        _viewModel.TextExpansion.Should().NotBeNull();
        _viewModel.Settings.Should().NotBeNull();
    }

    [Fact]
    public void RecordingStateChanged_UpdatesPlaybackAvailability()
    {
        var recordingProp = _recordingViewModel.GetType().GetProperty("IsRecording");

        recordingProp?.SetValue(_recordingViewModel, true);

        _playbackViewModel.CanPlayMacroExternal.Should().BeFalse();

        recordingProp?.SetValue(_recordingViewModel, false);

        _playbackViewModel.CanPlayMacroExternal.Should().BeTrue();
    }

    [Fact]
    public void PlaybackStateChanged_UpdatesRecordingAvailabilityAndFileManagement()
    {
        var playbackProp = _playbackViewModel.GetType().GetProperty("IsPlaying");

        playbackProp?.SetValue(_playbackViewModel, true);

        _recordingViewModel.CanStartRecordingExternal.Should().BeFalse();
        _filesViewModel.CanManageLoadedMacrosExternal.Should().BeFalse();

        playbackProp?.SetValue(_playbackViewModel, false);

        _recordingViewModel.CanStartRecordingExternal.Should().BeTrue();
        _filesViewModel.CanManageLoadedMacrosExternal.Should().BeTrue();
    }

    [Fact]
    public async Task MacroLoaded_UpdatesGlobalStatusAndSharedMacroSelection()
    {
        var macro = new MacroSequence
        {
            Name = "TestMacro",
            Events = { new MacroEvent { Type = EventType.MouseMove } }
        };

        _filesDialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/path/to/macro.macro"));
        _fileManager.LoadAsync("/path/to/macro.macro")
            .Returns(Task.FromResult<MacroSequence?>(macro));

        await _filesViewModel.LoadMacroAsync();

        _viewModel.GlobalStatus.Should().Be("Loaded: TestMacro");
        _filesViewModel.GetCurrentMacro().Should().BeSameAs(macro);
        _playbackViewModel.HasMacro.Should().BeTrue();
        _recordingViewModel.EventCount.Should().Be(1);
        _recordingViewModel.RecordingStatus.Should().Be("Loaded 1 events");
    }

    [Fact]
    public void FilesSelectionChanged_WhenIdle_UpdatesRecordingCountersAndStatus()
    {
        var firstMacro = CreateMacro("First", EventType.MouseMove, EventType.KeyPress);
        var secondMacro = CreateMacro("Second", EventType.ButtonPress, EventType.ButtonRelease, EventType.MouseMove, EventType.KeyPress);

        _filesViewModel.SetMacro(firstMacro);
        _filesViewModel.SetMacro(secondMacro);

        _filesViewModel.SelectedMacroItem = _filesViewModel.LoadedMacros[0];
        _recordingViewModel.EventCount.Should().Be(2);
        _recordingViewModel.MouseEventCount.Should().Be(1);
        _recordingViewModel.KeyboardEventCount.Should().Be(1);
        _recordingViewModel.RecordingStatus.Should().Be("Loaded 2 events");

        _filesViewModel.SelectedMacroItem = _filesViewModel.LoadedMacros[1];
        _recordingViewModel.EventCount.Should().Be(4);
        _recordingViewModel.MouseEventCount.Should().Be(3);
        _recordingViewModel.KeyboardEventCount.Should().Be(1);
        _recordingViewModel.RecordingStatus.Should().Be("Loaded 4 events");
    }

    [Fact]
    public void FilesSelectionChanged_WhenRecordingActive_DoesNotOverwriteRecordingCounters()
    {
        var firstMacro = CreateMacro("First", EventType.MouseMove, EventType.KeyPress, EventType.KeyRelease);
        var secondMacro = CreateMacro("Second", EventType.ButtonPress, EventType.ButtonRelease, EventType.MouseMove, EventType.KeyPress);

        _filesViewModel.SetMacro(firstMacro);
        _filesViewModel.SetMacro(secondMacro);

        var firstItem = _filesViewModel.LoadedMacros[0];
        var secondItem = _filesViewModel.LoadedMacros[1];
        _filesViewModel.SelectedMacroItem = firstItem;

        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.EventCount), 7);
        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.MouseEventCount), 3);
        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.KeyboardEventCount), 4);
        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.IsRecording), true);

        _filesViewModel.SelectedMacroItem = secondItem;

        _recordingViewModel.EventCount.Should().Be(7);
        _recordingViewModel.MouseEventCount.Should().Be(3);
        _recordingViewModel.KeyboardEventCount.Should().Be(4);
    }

    [Fact]
    public void RenameSelectedLoadedMacro_DoesNotRewriteRecordingStatusOrCounters()
    {
        var macro = CreateMacro("Original", EventType.MouseMove, EventType.KeyPress, EventType.KeyRelease);
        _filesViewModel.SetMacro(macro);
        _recordingViewModel.RecordingStatus = "Recorded 99 events";
        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.EventCount), 99);
        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.MouseEventCount), 40);
        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.KeyboardEventCount), 59);

        _filesViewModel.MacroName = "Renamed Macro";

        _filesViewModel.SelectedMacroItem!.Name.Should().Be("Renamed Macro");
        _recordingViewModel.RecordingStatus.Should().Be("Recorded 99 events");
        _recordingViewModel.EventCount.Should().Be(99);
        _recordingViewModel.MouseEventCount.Should().Be(40);
        _recordingViewModel.KeyboardEventCount.Should().Be(59);
    }

    [Fact]
    public void RecordingCompleted_WhenMacroIsAutoSelected_PreservesRecordedStatus()
    {
        var recordedMacro = CreateMacro("RecordedMacro", EventType.MouseMove, EventType.KeyPress);
        _recorder.StopRecording().Returns(recordedMacro);
        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.IsRecording), true);
        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.EventCount), 2);
        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.MouseEventCount), 1);
        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.KeyboardEventCount), 1);

        var result = _recordingViewModel.StopRecording();

        result.Should().BeSameAs(recordedMacro);
        _filesViewModel.GetCurrentMacro().Should().BeSameAs(recordedMacro);
        _recordingViewModel.RecordingStatus.Should().Be("Recorded 2 events");
    }

    [Fact]
    public async Task RemovingLastLoadedMacro_ClearsRecordingCounters()
    {
        var macro = CreateMacro("LoadedMacro", EventType.MouseMove, EventType.ButtonPress, EventType.KeyPress);
        _filesViewModel.SetMacro(macro);
        _filesDialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        _recordingViewModel.EventCount.Should().Be(3);
        _recordingViewModel.MouseEventCount.Should().Be(2);
        _recordingViewModel.KeyboardEventCount.Should().Be(1);

        await _filesViewModel.RemoveLoadedMacroCommand.ExecuteAsync(_filesViewModel.SelectedMacroItem);

        _recordingViewModel.EventCount.Should().Be(0);
        _recordingViewModel.MouseEventCount.Should().Be(0);
        _recordingViewModel.KeyboardEventCount.Should().Be(0);
        _recordingViewModel.RecordingStatus.Should().Be("Ready");
    }

    [Fact]
    public void EditorMacroCreated_WhenSavingDifferentEditorDocument_DoesNotOverwriteSelectedLoadedMacro()
    {
        var editorMacro = CreateMacro("Editor Macro", EventType.MouseMove, EventType.KeyPress);
        var editorMacroUpdated = CreateMacro("Editor Macro Updated", EventType.ButtonPress, EventType.KeyPress);
        var unrelatedSelectedMacro = CreateMacro("Selected Macro", EventType.ButtonRelease, EventType.KeyRelease);

        RaiseEditorMacroCreated(editorMacro);
        var trackedEditorItem = _filesViewModel.SelectedMacroItem;
        trackedEditorItem.Should().NotBeNull();

        _filesViewModel.SetMacro(unrelatedSelectedMacro);
        var selectedItem = _filesViewModel.SelectedMacroItem;
        selectedItem.Should().NotBeNull();
        selectedItem.Should().NotBeSameAs(trackedEditorItem);

        RaiseEditorMacroCreated(editorMacroUpdated);

        _filesViewModel.LoadedMacros.Should().HaveCount(2);
        trackedEditorItem!.Macro.Should().BeSameAs(editorMacroUpdated);
        trackedEditorItem.Name.Should().Be("Editor Macro Updated");
        selectedItem!.Macro.Should().BeSameAs(unrelatedSelectedMacro);
        selectedItem.Name.Should().Be("Selected Macro");
        _filesViewModel.SelectedMacroItem.Should().BeSameAs(selectedItem);
    }

    [Fact]
    public void EditorMacroCreated_WhenSavingSameMacroAgain_DoesNotAppendDuplicateLoadedItem()
    {
        var firstMacro = CreateMacro("Editor Macro", EventType.MouseMove, EventType.KeyPress);
        var updatedMacro = CreateMacro("Editor Macro Updated", EventType.ButtonPress, EventType.ButtonRelease, EventType.KeyPress);

        RaiseEditorMacroCreated(firstMacro);
        var selectedItem = _filesViewModel.SelectedMacroItem;
        selectedItem.Should().NotBeNull();
        selectedItem!.SequenceRepeatCount = 4;

        RaiseEditorMacroCreated(updatedMacro);

        _filesViewModel.LoadedMacros.Should().ContainSingle();
        _filesViewModel.SelectedMacroItem.Should().BeSameAs(selectedItem);
        _filesViewModel.SelectedMacroItem!.Macro.Should().BeSameAs(updatedMacro);
        _filesViewModel.SelectedMacroItem.Name.Should().Be("Editor Macro Updated");
        _filesViewModel.SelectedMacroItem.SequenceRepeatCount.Should().Be(4);
    }

    [Fact]
    public void EditorMacroCreated_WhenSavePathChanges_UpdatesLoadedMacroSourcePath()
    {
        var firstMacro = CreateMacro("Editor Macro", EventType.MouseMove, EventType.KeyPress);
        var updatedMacro = CreateMacro("Editor Macro Updated", EventType.ButtonPress, EventType.KeyPress);

        RaiseEditorMacroCreated(firstMacro, "/tmp/editor-original.macro");
        var item = _filesViewModel.SelectedMacroItem;

        item.Should().NotBeNull();
        item!.SourcePath.Should().Be("/tmp/editor-original.macro");

        RaiseEditorMacroCreated(updatedMacro, "/tmp/editor-save-as.macro");

        _filesViewModel.LoadedMacros.Should().ContainSingle();
        item.Macro.Should().BeSameAs(updatedMacro);
        item.SourcePath.Should().Be("/tmp/editor-save-as.macro");
        item.Description.Should().Contain("editor-save-as.macro");
    }

    [Fact]
    public async Task StopPlayback_WhenSequenceCleanupStillRunning_KeepsFilesLockedUntilPlaybackTaskFinishes()
    {
        var first = CreateMacro("First", EventType.MouseMove);
        var second = CreateMacro("Second", EventType.KeyPress);
        _filesViewModel.SetMacro(first);
        _filesViewModel.SetMacro(second);
        _filesViewModel.IsSequentialCycleMode = true;
        _filesViewModel.SelectedMacroItem = _filesViewModel.LoadedMacros[0];

        var playStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(async _ =>
            {
                playStarted.TrySetResult(true);
                await allowCompletion.Task;
            });

        var playTask = _playbackViewModel.PlayMacroAsync();
        await playStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        _filesViewModel.CanManageLoadedMacrosExternal.Should().BeFalse();

        _playbackViewModel.StopPlayback();

        _filesViewModel.CanManageLoadedMacrosExternal.Should().BeFalse();

        allowCompletion.TrySetResult(true);
        await playTask;

        _filesViewModel.CanManageLoadedMacrosExternal.Should().BeTrue();
    }

    [Fact]
    public void DismissUpdateNotification_HidesNotification()
    {
        _viewModel.IsUpdateNotificationVisible = true;

        _viewModel.DismissUpdateNotification();

        _viewModel.IsUpdateNotificationVisible.Should().BeFalse();
    }

    [Fact]
    public void OpenUpdateUrl_AlwaysDismissesNotification()
    {
        _viewModel.IsUpdateNotificationVisible = true;

        _viewModel.OpenUpdateUrl();

        _viewModel.IsUpdateNotificationVisible.Should().BeFalse();
    }

    [Theory]
    [InlineData(DisplayEnvironment.LinuxX11)]
    [InlineData(DisplayEnvironment.LinuxWayland)]
    [InlineData(DisplayEnvironment.LinuxHyprland)]
    [InlineData(DisplayEnvironment.LinuxKDE)]
    [InlineData(DisplayEnvironment.LinuxGnome)]
    public void GetBackendTroubleshootingHint_WhenLinuxEnvironment_ReturnsSystemctlGuidance(DisplayEnvironment environment)
    {
        var hint = GetBackendTroubleshootingHint(environment);

        hint.Should().NotBeNull();
        hint.Should().Contain("systemctl status crossmacro");
    }

    [Theory]
    [InlineData(DisplayEnvironment.Windows)]
    [InlineData(DisplayEnvironment.MacOS)]
    public void GetBackendTroubleshootingHint_WhenNonLinuxEnvironment_DoesNotReturnLinuxCommand(DisplayEnvironment environment)
    {
        var hint = GetBackendTroubleshootingHint(environment);

        hint.Should().NotBeNull();
        hint.Should().NotContain("systemctl status crossmacro");
    }

    [Fact]
    public void GetBackendTroubleshootingHint_WhenUnknownEnvironment_ReturnsNull()
    {
        var hint = GetBackendTroubleshootingHint(DisplayEnvironment.Unknown);

        hint.Should().BeNull();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var act = () =>
        {
            _viewModel.Dispose();
            _viewModel.Dispose();
        };

        act.Should().NotThrow();
    }

    private static string? GetBackendTroubleshootingHint(DisplayEnvironment environment)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "GetBackendTroubleshootingHint",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();
        return (string?)method!.Invoke(null, [environment]);
    }

    private void RaiseEditorMacroCreated(MacroSequence macro, string sourcePath = "/tmp/editor-test.macro")
    {
        var field = _viewModel.Editor.GetType().GetField(
            "MacroCreated",
            BindingFlags.Instance | BindingFlags.NonPublic);

        field.Should().NotBeNull();
        var handler = field!.GetValue(_viewModel.Editor) as EventHandler<EditorMacroCreatedEventArgs>;
        handler.Should().NotBeNull();
        handler!.Invoke(_viewModel.Editor, new EditorMacroCreatedEventArgs(macro, sourcePath));
    }

    private static MacroSequence CreateMacro(string name, params EventType[] eventTypes)
    {
        var macro = new MacroSequence { Name = name };
        foreach (var eventType in eventTypes)
        {
            macro.Events.Add(new MacroEvent { Type = eventType });
        }

        return macro;
    }

    private static void SetPrivateProperty<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        property.Should().NotBeNull();
        property!.SetValue(target, value);
    }
}
