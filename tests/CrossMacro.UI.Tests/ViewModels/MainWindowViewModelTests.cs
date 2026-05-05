using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
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
    private readonly ILocalizationService _localizationService;
    private readonly LoadedMacroSession _loadedMacroSession;

    private readonly RecordingViewModel _recordingViewModel;
    private readonly PlaybackViewModel _playbackViewModel;
    private readonly FilesViewModel _filesViewModel;
    private readonly TextExpansionViewModel _textExpansionViewModel;
    private readonly ScheduleViewModel _scheduleViewModel;
    private readonly ShortcutViewModel _shortcutViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly EditorViewModel _editorViewModel;

    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        _settingsService = Substitute.For<ISettingsService>();
        _settingsService.Current.Returns(new AppSettings());
        var runtimeContext = Substitute.For<IRuntimeContext>();
        runtimeContext.IsLinux.Returns(true);
        _localizationService = Substitute.For<ILocalizationService>();
        _localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "Recording_StatusReady" => "[Recording_StatusReady]",
            "Recording_StatusRecording" => "[Recording_StatusRecording]",
            "Recording_StatusLoadedEvents" => "[Recording_StatusLoadedEvents] {0}",
            "Recording_StatusRecordedEvents" => "[Recording_StatusRecordedEvents] {0}",
            "Files_StatusReady" => "[Files_StatusReady]",
            "Files_UnnamedMacro" => "[Files_UnnamedMacro]",
            "Files_SourceSession" => "[Files_SourceSession]",
            "Files_SequenceRepeatSummary" => "[Files_SequenceRepeatSummary] {0}",
            "Files_LoadedMacroDescription" => "[Files_LoadedMacroDescription] {0} | {1}",
            "Files_StatusLoaded" => "[Files_StatusLoaded] {0}",
            "Status_Ready" => "[Status_Ready]",
            "Status_LoadedMacro" => "[Status_LoadedMacro] {0}",
            "Status_RecordedEvents" => "[Status_RecordedEvents] {0}",
            "Status_CreatedMacro" => "[Status_CreatedMacro] {0} ({1})",
            "MainWindow_UpdateAvailableVersion" => "v{0} is available",
            "Navigation_Recording" => "[Navigation_Recording]",
            "Navigation_Playback" => "[Navigation_Playback]",
            "Navigation_Files" => "[Navigation_Files]",
            "Navigation_TextExpansion" => "[Navigation_TextExpansion]",
            "Navigation_Shortcuts" => "[Navigation_Shortcuts]",
            "Navigation_Schedule" => "[Navigation_Schedule]",
            "Navigation_Editor" => "[Navigation_Editor]",
            "Navigation_Settings" => "[Navigation_Settings]",
            _ => call.Arg<string>()
        });

        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
        _positionProvider = Substitute.For<IMousePositionProvider>();
        _loadedMacroSession = new LoadedMacroSession(_localizationService);

        _recorder = Substitute.For<IMacroRecorder>();
        _recordingViewModel = new RecordingViewModel(_recorder, _hotkeyService, _settingsService, _localizationService, runtimeContext);

        _player = Substitute.For<IMacroPlayer>();
        _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.CompletedTask);
        _playbackViewModel = new PlaybackViewModel(_player, _settingsService, _loadedMacroSession);

        _fileManager = Substitute.For<IMacroFileManager>();
        _filesDialogService = Substitute.For<IDialogService>();
        _externalUrlOpener = Substitute.For<IExternalUrlOpener>();
        _filesViewModel = new FilesViewModel(_fileManager, _filesDialogService, _loadedMacroSession, _localizationService);

        var textExpansionStorage = Substitute.For<ITextExpansionStorageService>();
        var dialogService = Substitute.For<IDialogService>();
        var environmentInfo = Substitute.For<IEnvironmentInfoProvider>();
        environmentInfo.WindowManagerHandlesCloseButton.Returns(false);
        environmentInfo.CurrentEnvironment.Returns(DisplayEnvironment.Windows);

        _textExpansionViewModel = new TextExpansionViewModel(textExpansionStorage, dialogService, environmentInfo, _localizationService);

        _schedulerService = Substitute.For<ISchedulerService>();
        _schedulerService.LoadAsync().Returns(Task.CompletedTask);
        var timeProvider = Substitute.For<ITimeProvider>();
        timeProvider.Now.Returns(new DateTime(2026, 1, 1, 10, 0, 0));
        timeProvider.UtcNow.Returns(new DateTime(2026, 1, 1, 7, 0, 0));
        _scheduleViewModel = new ScheduleViewModel(_schedulerService, dialogService, timeProvider, _localizationService);

        _shortcutService = Substitute.For<IShortcutService>();
        _shortcutViewModel = new ShortcutViewModel(_shortcutService, dialogService, _hotkeyService, _localizationService);

        var hotkeySettings = new HotkeySettings();
        var textExpansionService = Substitute.For<ITextExpansionService>();
        var runtimeLogLevelService = Substitute.For<IRuntimeLogLevelService>();
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
            runtimeLogLevelService,
            themeService);

        var editorConverter = Substitute.For<IEditorActionConverter>();
        var editorValidator = Substitute.For<IEditorActionValidator>();
        var captureService = Substitute.For<ICoordinateCaptureService>();
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _editorViewModel = new EditorViewModel(editorConverter, editorValidator, captureService, _fileManager, dialogService, keyCodeMapper);

        _viewModel = new MainWindowViewModel(
            _recordingViewModel,
            _playbackViewModel,
            _filesViewModel,
            _textExpansionViewModel,
            _scheduleViewModel,
            _shortcutViewModel,
            _settingsViewModel,
            _editorViewModel,
            _hotkeyService,
            _positionProvider,
            environmentInfo,
            _externalUrlOpener,
            _localizationService,
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
    public void Construction_SelectsRecordingAsStartupPage()
    {
        _viewModel.SelectedTopItem.Should().BeSameAs(_viewModel.TopNavigationItems[0]);
        _viewModel.SelectedBottomItem.Should().BeNull();
        _viewModel.SelectedNavigationItem.Should().BeSameAs(_viewModel.TopNavigationItems[0]);
        _viewModel.CurrentPage.Should().BeSameAs(_recordingViewModel);
    }

    [Fact]
    public void Construction_WhenExtensionWarningWasPublishedBeforeSubscription_ShowsWarningBannerAndNotification()
    {
        var notifier = new FakeExtensionStatusNotifier();
        notifier.Publish(ExtensionStatusCode.Warning, "Please enable GNOME extension manually or restart your session");

        using var viewModel = CreateMainWindowViewModel(extensionNotifier: notifier);

        viewModel.HasExtensionWarning.Should().BeTrue();
        viewModel.ExtensionWarning.Should().Be("Please enable GNOME extension manually or restart your session");
        viewModel.IsAppNotificationVisible.Should().BeTrue();
        viewModel.AppNotificationTitle.Should().Be("GNOME Extension");
        viewModel.AppNotificationMessage.Should().Be("Please enable GNOME extension manually or restart your session");
        viewModel.IsAppNotificationWarning.Should().BeTrue();
    }

    [Fact]
    public void ExtensionStatusUpdated_WhenWarningPublishedAfterSubscription_ShowsWarningBannerAndNotification()
    {
        var notifier = new FakeExtensionStatusNotifier();
        using var viewModel = CreateMainWindowViewModel(extensionNotifier: notifier);

        notifier.Publish(ExtensionStatusCode.Warning, "GNOME extension requires logout/login to activate");

        viewModel.HasExtensionWarning.Should().BeTrue();
        viewModel.ExtensionWarning.Should().Be("GNOME extension requires logout/login to activate");
        viewModel.IsAppNotificationVisible.Should().BeTrue();
        viewModel.AppNotificationMessage.Should().Be("GNOME extension requires logout/login to activate");
        viewModel.IsAppNotificationWarning.Should().BeTrue();
    }

    [Fact]
    public void Dispose_WhenAppNotificationTimerIsActive_DoesNotThrow()
    {
        var notifier = new FakeExtensionStatusNotifier();
        var viewModel = CreateMainWindowViewModel(extensionNotifier: notifier);
        notifier.Publish(ExtensionStatusCode.Warning, "GNOME extension requires logout/login to activate");

        var act = viewModel.Dispose;

        act.Should().NotThrow();
    }

    [Fact]
    public void ExtensionStatusUpdated_WhenErrorPublishedAfterSubscription_ShowsErrorNotification()
    {
        var notifier = new FakeExtensionStatusNotifier();
        using var viewModel = CreateMainWindowViewModel(extensionNotifier: notifier);

        notifier.Publish(ExtensionStatusCode.Error, "Failed to install GNOME extension");

        viewModel.HasExtensionWarning.Should().BeTrue();
        viewModel.ExtensionWarning.Should().Be("Failed to install GNOME extension");
        viewModel.IsAppNotificationVisible.Should().BeTrue();
        viewModel.AppNotificationMessage.Should().Be("Failed to install GNOME extension");
        viewModel.IsAppNotificationError.Should().BeTrue();
        viewModel.IsAppNotificationWarning.Should().BeFalse();
    }

    [Fact]
    public void NavigationCatalog_CreatesExpectedNavigationMetadataAndPages()
    {
        var catalog = new MainWindowNavigationCatalog(_localizationService);

        var topItems = catalog.CreateTopItems(
            _recordingViewModel,
            _playbackViewModel,
            _filesViewModel,
            _textExpansionViewModel,
            _shortcutViewModel,
            _scheduleViewModel,
            _editorViewModel);
        var bottomItems = catalog.CreateBottomItems(_settingsViewModel);

        topItems.Select(item => (item.LocalizationKey, item.Label, item.ViewModel)).Should().Equal(
        [
            ("Navigation_Recording", "[Navigation_Recording]", _recordingViewModel),
            ("Navigation_Playback", "[Navigation_Playback]", _playbackViewModel),
            ("Navigation_Files", "[Navigation_Files]", _filesViewModel),
            ("Navigation_TextExpansion", "[Navigation_TextExpansion]", _textExpansionViewModel),
            ("Navigation_Shortcuts", "[Navigation_Shortcuts]", _shortcutViewModel),
            ("Navigation_Schedule", "[Navigation_Schedule]", _scheduleViewModel),
            ("Navigation_Editor", "[Navigation_Editor]", _editorViewModel)
        ]);
        topItems.Should().OnlyContain(item => Enum.IsDefined(item.Icon));

        bottomItems.Select(item => (item.LocalizationKey, item.Label, item.ViewModel)).Should().Equal(
        [
            ("Navigation_Settings", "[Navigation_Settings]", _settingsViewModel)
        ]);
        bottomItems.Should().OnlyContain(item => Enum.IsDefined(item.Icon));
    }

    [Fact]
    public void NavigationCatalog_RefreshLabels_UpdatesLabelsByLocalizationKey()
    {
        var catalog = new MainWindowNavigationCatalog(_localizationService);
        var topItems = catalog.CreateTopItems(
            _recordingViewModel,
            _playbackViewModel,
            _filesViewModel,
            _textExpansionViewModel,
            _shortcutViewModel,
            _scheduleViewModel,
            _editorViewModel);
        var bottomItems = catalog.CreateBottomItems(_settingsViewModel);
        _localizationService["Navigation_Recording"].Returns("[Navigation_Recording:updated]");
        _localizationService["Navigation_Settings"].Returns("[Navigation_Settings:updated]");

        catalog.RefreshLabels(topItems, bottomItems);

        topItems[0].Label.Should().Be("[Navigation_Recording:updated]");
        bottomItems[0].Label.Should().Be("[Navigation_Settings:updated]");
    }

    [Fact]
    public async Task Construction_StartsOwnedShellInitializationTask()
    {
        var schedulerGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updateGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var schedulerService = Substitute.For<ISchedulerService>();
        schedulerService.LoadAsync().Returns(async _ => await schedulerGate.Task);

        var updateService = Substitute.For<IUpdateService>();
        updateService.CheckForUpdatesAsync().Returns(async _ =>
        {
            await updateGate.Task;
            return new UpdateCheckResult
            {
                HasUpdate = true,
                LatestVersion = "9.9.9",
                ReleaseUrl = "https://example.invalid/releases/9.9.9"
            };
        });

        var viewModel = CreateMainWindowViewModel(
            schedulerService: schedulerService,
            updateService: updateService,
            checkForUpdates: true);

        viewModel.StartupInitializationTask.IsCompleted.Should().BeFalse();

        schedulerGate.SetResult(true);
        updateGate.SetResult(true);

        await viewModel.StartupInitializationTask;

        await schedulerService.Received(1).LoadAsync();
        await updateService.Received(1).CheckForUpdatesAsync();
        viewModel.LatestVersion.Should().Be("9.9.9");
        viewModel.IsUpdateNotificationVisible.Should().BeTrue();
    }

    [Fact]
    public async Task Construction_WhenScheduleInitializationHandlesFailure_StartupTaskStillCompletesAndContinuesUpdateCheck()
    {
        var schedulerService = Substitute.For<ISchedulerService>();
        schedulerService.LoadAsync().Returns(Task.FromException(new InvalidOperationException("scheduler boom")));

        var updateService = Substitute.For<IUpdateService>();
        updateService.CheckForUpdatesAsync().Returns(Task.FromResult(new UpdateCheckResult
        {
            HasUpdate = true,
            LatestVersion = "1.2.3",
            ReleaseUrl = "https://example.invalid/releases/1.2.3"
        }));

        var viewModel = CreateMainWindowViewModel(
            schedulerService: schedulerService,
            updateService: updateService,
            checkForUpdates: true);

        await viewModel.StartupInitializationTask;

        _ = schedulerService.Received(1).LoadAsync();
        await updateService.Received(1).CheckForUpdatesAsync();
        viewModel.LatestVersion.Should().Be("1.2.3");
        viewModel.IsUpdateNotificationVisible.Should().BeTrue();
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

        _viewModel.GlobalStatus.Should().Be("[Status_LoadedMacro] TestMacro");
        _filesViewModel.GetCurrentMacro().Should().BeSameAs(macro);
        _playbackViewModel.HasMacro.Should().BeTrue();
        _recordingViewModel.EventCount.Should().Be(1);
        _recordingViewModel.RecordingStatus.Should().Be("[Recording_StatusLoadedEvents] 1");
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
        _recordingViewModel.RecordingStatus.Should().Be("[Recording_StatusLoadedEvents] 2");

        _filesViewModel.SelectedMacroItem = _filesViewModel.LoadedMacros[1];
        _recordingViewModel.EventCount.Should().Be(4);
        _recordingViewModel.MouseEventCount.Should().Be(3);
        _recordingViewModel.KeyboardEventCount.Should().Be(1);
        _recordingViewModel.RecordingStatus.Should().Be("[Recording_StatusLoadedEvents] 4");
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
        _recordingViewModel.RecordingStatus = "[Recording_StatusRecordedEvents] 99";
        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.EventCount), 99);
        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.MouseEventCount), 40);
        SetPrivateProperty(_recordingViewModel, nameof(RecordingViewModel.KeyboardEventCount), 59);

        _filesViewModel.MacroName = "Renamed Macro";

        _filesViewModel.SelectedMacroItem!.Name.Should().Be("Renamed Macro");
        _recordingViewModel.RecordingStatus.Should().Be("[Recording_StatusRecordedEvents] 99");
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
        _recordingViewModel.RecordingStatus.Should().Be("[Recording_StatusRecordedEvents] 2");
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
        _recordingViewModel.RecordingStatus.Should().Be("[Recording_StatusReady]");
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

    [Fact]
    public void DismissUpdateNotificationCommand_ExecutesBoundDismissAction()
    {
        _viewModel.IsUpdateNotificationVisible = true;

        _viewModel.DismissUpdateNotificationCommand.Execute(null);

        _viewModel.IsUpdateNotificationVisible.Should().BeFalse();
    }

    [Fact]
    public void OpenUpdateUrlCommand_ExecutesBoundOpenActionAndDismissesNotification()
    {
        SetPrivateField(_viewModel, "_updateReleaseUrl", "https://example.invalid/releases/latest");
        _viewModel.IsUpdateNotificationVisible = true;

        _viewModel.OpenUpdateUrlCommand.Execute(null);

        _externalUrlOpener.Received(1).Open("https://example.invalid/releases/latest");
        _viewModel.IsUpdateNotificationVisible.Should().BeFalse();
    }

    [Fact]
    public void CultureChanged_RefreshesNavigationLabels_ByLocalizationKey()
    {
        _viewModel.TopNavigationItems[0].LocalizationKey.Should().Be("Navigation_Recording");
        _viewModel.BottomNavigationItems[0].LocalizationKey.Should().Be("Navigation_Settings");

        _localizationService["Navigation_Recording"].Returns("[Navigation_Recording:updated]");
        _localizationService["Navigation_Settings"].Returns("[Navigation_Settings:updated]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _viewModel.TopNavigationItems[0].Label.Should().Be("[Navigation_Recording:updated]");
        _viewModel.BottomNavigationItems[0].Label.Should().Be("[Navigation_Settings:updated]");
    }

    [Fact]
    public void CultureChanged_WhenIdleAndNoMacro_RefreshesGlobalReadyStatus()
    {
        _viewModel.GlobalStatus = "[Status_Ready]";
        _localizationService["Status_Ready"].Returns("[Status_Ready:updated]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _viewModel.GlobalStatus.Should().Be("[Status_Ready:updated]");
    }

    [Fact]
    public void CultureChanged_WhenIdleWithMacro_RefreshesGlobalStatusFromRecordingSummary()
    {
        var macro = CreateMacro("Macro", EventType.MouseMove, EventType.KeyPress);
        _filesViewModel.SetMacro(macro);
        _localizationService["Recording_StatusLoadedEvents"].Returns("[Recording_StatusLoadedEvents:updated] {0}");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _viewModel.GlobalStatus.Should().Be("[Recording_StatusLoadedEvents:updated] 2");
    }

    [Theory]
    [InlineData(DisplayEnvironment.LinuxX11)]
    [InlineData(DisplayEnvironment.LinuxWayland)]
    [InlineData(DisplayEnvironment.LinuxHyprland)]
    [InlineData(DisplayEnvironment.LinuxWayfire)]
    [InlineData(DisplayEnvironment.LinuxKDE)]
    [InlineData(DisplayEnvironment.LinuxGnome)]
    public void GetBackendTroubleshootingHint_WhenLinuxEnvironment_ReturnsSystemctlGuidance(DisplayEnvironment environment)
    {
        var hint = GetBackendTroubleshootingHint(environment);

        hint.Should().NotBeNull();
        hint.Should().Contain("systemctl status crossmacro.service");
        hint.Should().Contain("direct device mode");
    }

    [Theory]
    [InlineData(DisplayEnvironment.Windows)]
    [InlineData(DisplayEnvironment.MacOS)]
    public void GetBackendTroubleshootingHint_WhenNonLinuxEnvironment_DoesNotReturnLinuxCommand(DisplayEnvironment environment)
    {
        var hint = GetBackendTroubleshootingHint(environment);

        hint.Should().NotBeNull();
        hint.Should().NotContain("systemctl status crossmacro.service");
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

    private MainWindowViewModel CreateMainWindowViewModel(
        ISchedulerService? schedulerService = null,
        IUpdateService? updateService = null,
        bool? checkForUpdates = null,
        IExtensionStatusNotifier? extensionNotifier = null)
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Current.Returns(new AppSettings
        {
            CheckForUpdates = checkForUpdates ?? false
        });
        var runtimeContext = Substitute.For<IRuntimeContext>();
        runtimeContext.IsLinux.Returns(true);

        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "Recording_StatusReady" => "[Recording_StatusReady]",
            "Recording_StatusRecording" => "[Recording_StatusRecording]",
            "Recording_StatusLoadedEvents" => "[Recording_StatusLoadedEvents] {0}",
            "Recording_StatusRecordedEvents" => "[Recording_StatusRecordedEvents] {0}",
            "Files_StatusReady" => "[Files_StatusReady]",
            "Files_UnnamedMacro" => "[Files_UnnamedMacro]",
            "Files_SourceSession" => "[Files_SourceSession]",
            "Files_SequenceRepeatSummary" => "[Files_SequenceRepeatSummary] {0}",
            "Files_LoadedMacroDescription" => "[Files_LoadedMacroDescription] {0} | {1}",
            "Files_StatusLoaded" => "[Files_StatusLoaded] {0}",
            "Status_Ready" => "[Status_Ready]",
            "Status_LoadedMacro" => "[Status_LoadedMacro] {0}",
            "Status_RecordedEvents" => "[Status_RecordedEvents] {0}",
            "Status_CreatedMacro" => "[Status_CreatedMacro] {0} ({1})",
            "MainWindow_UpdateAvailableVersion" => "v{0} is available",
            "Navigation_Recording" => "[Navigation_Recording]",
            "Navigation_Playback" => "[Navigation_Playback]",
            "Navigation_Files" => "[Navigation_Files]",
            "Navigation_TextExpansion" => "[Navigation_TextExpansion]",
            "Navigation_Shortcuts" => "[Navigation_Shortcuts]",
            "Navigation_Schedule" => "[Navigation_Schedule]",
            "Navigation_Editor" => "[Navigation_Editor]",
            "Navigation_Settings" => "[Navigation_Settings]",
            _ => call.Arg<string>()
        });

        var hotkeyService = Substitute.For<IGlobalHotkeyService>();
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var loadedMacroSession = new LoadedMacroSession(localizationService);
        var recorder = Substitute.For<IMacroRecorder>();
        var recordingViewModel = new RecordingViewModel(recorder, hotkeyService, settingsService, localizationService, runtimeContext);

        var player = Substitute.For<IMacroPlayer>();
        player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var playbackViewModel = new PlaybackViewModel(player, settingsService, loadedMacroSession);

        var fileManager = Substitute.For<IMacroFileManager>();
        var filesDialogService = Substitute.For<IDialogService>();
        var externalUrlOpener = Substitute.For<IExternalUrlOpener>();
        var filesViewModel = new FilesViewModel(fileManager, filesDialogService, loadedMacroSession, localizationService);

        var textExpansionStorage = Substitute.For<ITextExpansionStorageService>();
        var dialogService = Substitute.For<IDialogService>();
        var environmentInfo = Substitute.For<IEnvironmentInfoProvider>();
        environmentInfo.WindowManagerHandlesCloseButton.Returns(false);
        environmentInfo.CurrentEnvironment.Returns(DisplayEnvironment.Windows);
        var textExpansionViewModel = new TextExpansionViewModel(textExpansionStorage, dialogService, environmentInfo, localizationService);

        if (schedulerService is null)
        {
            schedulerService = Substitute.For<ISchedulerService>();
            schedulerService.LoadAsync().Returns(Task.CompletedTask);
        }

        var timeProvider = Substitute.For<ITimeProvider>();
        timeProvider.Now.Returns(new DateTime(2026, 1, 1, 10, 0, 0));
        timeProvider.UtcNow.Returns(new DateTime(2026, 1, 1, 7, 0, 0));
        var scheduleViewModel = new ScheduleViewModel(schedulerService, dialogService, timeProvider, localizationService);

        var shortcutService = Substitute.For<IShortcutService>();
        var shortcutViewModel = new ShortcutViewModel(shortcutService, dialogService, hotkeyService, localizationService);

        var hotkeySettings = new HotkeySettings();
        var textExpansionService = Substitute.For<ITextExpansionService>();
        var runtimeLogLevelService = Substitute.For<IRuntimeLogLevelService>();
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
        var settingsViewModel = new SettingsViewModel(
            hotkeyService,
            settingsService,
            textExpansionService,
            hotkeySettings,
            externalUrlOpener,
            runtimeLogLevelService,
            themeService,
            localizationService);

        var editorConverter = Substitute.For<IEditorActionConverter>();
        var editorValidator = Substitute.For<IEditorActionValidator>();
        var captureService = Substitute.For<ICoordinateCaptureService>();
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        var editorViewModel = new EditorViewModel(editorConverter, editorValidator, captureService, fileManager, dialogService, keyCodeMapper);

        return new MainWindowViewModel(
            recordingViewModel,
            playbackViewModel,
            filesViewModel,
            textExpansionViewModel,
            scheduleViewModel,
            shortcutViewModel,
            settingsViewModel,
            editorViewModel,
            hotkeyService,
            positionProvider,
            environmentInfo,
            externalUrlOpener,
            localizationService,
            extensionNotifier,
            updateService);
    }

    private sealed class FakeExtensionStatusNotifier : IExtensionStatusNotifier
    {
        public event EventHandler<ExtensionStatusChangedEventArgs>? ExtensionStatusUpdated;
        public event EventHandler<string>? ExtensionStatusChanged;

        public ExtensionStatusChangedEventArgs? CurrentExtensionStatus { get; private set; }

        public void Publish(ExtensionStatusCode code, string message)
        {
            var args = new ExtensionStatusChangedEventArgs(code, message);
            CurrentExtensionStatus = args;
            ExtensionStatusUpdated?.Invoke(this, args);
            ExtensionStatusChanged?.Invoke(this, message);
        }
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

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        field.Should().NotBeNull();
        field!.SetValue(target, value);
    }

}
