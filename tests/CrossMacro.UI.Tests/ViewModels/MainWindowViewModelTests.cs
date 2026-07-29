
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class MainWindowViewModelTests
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
    private readonly IEditorActionConverter _editorConverter;
    private readonly IEditorActionValidator _editorValidator;
    private readonly IDialogService _editorDialogService;

    private readonly RecordingViewModel _recordingViewModel;
    private readonly PlaybackViewModel _playbackViewModel;
    private readonly FilesViewModel _filesViewModel;
    private readonly TextExpansionViewModel _textExpansionViewModel;
    private readonly ScheduleViewModel _scheduleViewModel;
    private readonly ShortcutViewModel _shortcutViewModel;
    private readonly TriggerViewModel _triggerViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly EditorViewModel _editorViewModel;

    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        _settingsService = Substitute.For<ISettingsService>();
        _ = _settingsService.Current.Returns(new AppSettings());
        var runtimeContext = Substitute.For<IRuntimeContext>();
        _ = runtimeContext.IsLinux.Returns(returnThis: true);
        _localizationService = Substitute.For<ILocalizationService>();
        _ = _localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _ = _localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
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
            "MainWindow_GnomeExtensionTitle" => "[MainWindow_GnomeExtensionTitle]",
            "MainWindow_BackendErrorTitle" => "[MainWindow_BackendErrorTitle]",
            "MainWindow_BackendTroubleshootingFormat" => "Troubleshooting: {0}",
            "MainWindow_BackendTroubleshootingLinux" => "check `systemctl status crossmacro.service`; direct device mode may require Linux input permissions instead.",
            "MainWindow_BackendTroubleshootingWindows" => "restart CrossMacro and verify the background service is running.",
            "MainWindow_BackendTroubleshootingMacOS" => "restart CrossMacro and verify Input Monitoring and Accessibility permissions in System Settings.",
            "Navigation_Recording" => "[Navigation_Recording]",
            "Navigation_Playback" => "[Navigation_Playback]",
            "Navigation_Files" => "[Navigation_Files]",
            "Navigation_TextExpansion" => "[Navigation_TextExpansion]",
            "Navigation_Shortcuts" => "[Navigation_Shortcuts]",
            "Navigation_Schedule" => "[Navigation_Schedule]",
            "Navigation_Triggers" => "[Navigation_Triggers]",
            "Navigation_Editor" => "[Navigation_Editor]",
            "Navigation_Settings" => "[Navigation_Settings]",
            _ => call.Arg<string>(),
        });

        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
        _positionProvider = Substitute.For<IMousePositionProvider>();
        _loadedMacroSession = new LoadedMacroSession(_localizationService);

        _recorder = Substitute.For<IMacroRecorder>();
        _recordingViewModel = new RecordingViewModel(
            _recorder,
            _hotkeyService,
            _settingsService,
            _localizationService,
            runtimeContext,
            static action => action());

        _player = Substitute.For<IMacroPlayer>();
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(Task.CompletedTask);
        _playbackViewModel = new PlaybackViewModel(_player, _settingsService, _loadedMacroSession);

        _fileManager = Substitute.For<IMacroFileManager>();
        _filesDialogService = Substitute.For<IDialogService>();
        _externalUrlOpener = Substitute.For<IExternalUrlOpener>();
        _filesViewModel = new FilesViewModel(_fileManager, _filesDialogService, _loadedMacroSession, _localizationService);

        var textExpansionStorage = Substitute.For<ITextExpansionStore>();
        var dialogService = Substitute.For<IDialogService>();
        var environmentInfo = Substitute.For<IEnvironmentInfoProvider>();
        _ = environmentInfo.WindowManagerHandlesCloseButton.Returns(returnThis: false);
        _ = environmentInfo.CurrentEnvironment.Returns(DisplayEnvironment.Windows);

        _textExpansionViewModel = new TextExpansionViewModel(textExpansionStorage, dialogService, environmentInfo, _localizationService);

        _schedulerService = Substitute.For<ISchedulerService>();
        _ = _schedulerService.Tasks.Returns(new ObservableCollection<ScheduledTask>());
        _ = _schedulerService.LoadAsync().Returns(Task.CompletedTask);
        var timeProvider = Substitute.For<TimeProvider>();
        _ = timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 1, 1, 7, 0, 0, TimeSpan.Zero));
        _scheduleViewModel = new ScheduleViewModel(_schedulerService, dialogService, timeProvider, _localizationService);

        _shortcutService = Substitute.For<IShortcutService>();
        _ = _shortcutService.Tasks.Returns(new ObservableCollection<ShortcutTask>());
        _shortcutViewModel = new ShortcutViewModel(_shortcutService, dialogService, _hotkeyService, _localizationService);

        var triggerService = Substitute.For<ITriggerService>();
        _ = triggerService.Tasks.Returns(new System.Collections.ObjectModel.ObservableCollection<TriggerTask>());
        _ = triggerService.LoadAsync().Returns(Task.CompletedTask);
        _triggerViewModel = new TriggerViewModel(triggerService, profileManager: null, dialogService, _localizationService, windowManager: null);

        var hotkeySettings = new HotkeySettings();
        var textExpansionService = Substitute.For<ITextExpansionService>();
        var runtimeLogLevelService = Substitute.For<IRuntimeLogLevelService>();
        var themeService = Substitute.For<IThemeService>();
        _ = themeService.AvailableThemes.Returns(["Classic"]);
        _ = themeService.CurrentTheme.Returns("Classic");
        _ = themeService
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
            themeService,
            Substitute.For<IRuntimeContext>());

        _editorConverter = Substitute.For<IEditorActionConverter>();
        _editorValidator = Substitute.For<IEditorActionValidator>();
        var captureService = Substitute.For<ICoordinateCaptureService>();
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _editorDialogService = dialogService;
        _editorViewModel = new EditorViewModel(_editorConverter, _editorValidator, captureService, _fileManager, _editorDialogService, keyCodeMapper, Substitute.For<CrossMacro.Core.Services.IMacroPlayer>(), _localizationService);

        _viewModel = new MainWindowViewModel(
            _recordingViewModel,
            _playbackViewModel,
            _filesViewModel,
            _textExpansionViewModel,
            _scheduleViewModel,
            _shortcutViewModel,
            _triggerViewModel,
            _settingsViewModel,
            _editorViewModel,
            _hotkeyService,
            _positionProvider,
            environmentInfo,
            _externalUrlOpener,
            _localizationService,
extensionNotifier: null);
    }

    [Fact]
    public void Construction_InitializedChildViewModels()
    {
        _ = _viewModel.Recording.Should().NotBeNull();
        _ = _viewModel.Playback.Should().NotBeNull();
        _ = _viewModel.Files.Should().NotBeNull();
        _ = _viewModel.TextExpansion.Should().NotBeNull();
        _ = _viewModel.Settings.Should().NotBeNull();
    }

    [Fact]
    public void Construction_SelectsRecordingAsStartupPage()
    {
        _ = _viewModel.SelectedTopItem.Should().BeSameAs(_viewModel.TopNavigationItems[0]);
        _ = _viewModel.SelectedBottomItem.Should().BeNull();
        _ = _viewModel.SelectedNavigationItem.Should().BeSameAs(_viewModel.TopNavigationItems[0]);
        _ = _viewModel.CurrentPage.Should().BeSameAs(_recordingViewModel);
    }

    [Fact]
    public void Construction_WhenExtensionWarningWasPublishedBeforeSubscription_ShowsWarningBannerAndNotification()
    {
        var notifier = new FakeExtensionStatusNotifier();
        notifier.Publish(ExtensionStatusCode.Warning, "Please enable GNOME extension manually or restart your session");

        using var viewModel = CreateMainWindowViewModel(extensionNotifier: notifier);

        _ = viewModel.HasExtensionWarning.Should().BeTrue();
        _ = viewModel.ExtensionWarning.Should().Be("Please enable GNOME extension manually or restart your session");
        _ = viewModel.IsAppNotificationVisible.Should().BeTrue();
        _ = viewModel.AppNotificationTitle.Should().Be("[MainWindow_GnomeExtensionTitle]");
        _ = viewModel.AppNotificationMessage.Should().Be("Please enable GNOME extension manually or restart your session");
        _ = viewModel.IsAppNotificationWarning.Should().BeTrue();
    }

    [Fact]
    public void ExtensionStatusUpdated_WhenWarningPublishedAfterSubscription_ShowsWarningBannerAndNotification()
    {
        var notifier = new FakeExtensionStatusNotifier();
        using var viewModel = CreateMainWindowViewModel(extensionNotifier: notifier);

        notifier.Publish(ExtensionStatusCode.Warning, "GNOME extension requires logout/login to activate");

        _ = viewModel.HasExtensionWarning.Should().BeTrue();
        _ = viewModel.ExtensionWarning.Should().Be("GNOME extension requires logout/login to activate");
        _ = viewModel.IsAppNotificationVisible.Should().BeTrue();
        _ = viewModel.AppNotificationMessage.Should().Be("GNOME extension requires logout/login to activate");
        _ = viewModel.IsAppNotificationWarning.Should().BeTrue();
    }

    [Fact]
    public void Dispose_WhenAppNotificationTimerIsActive_DoesNotThrow()
    {
        var notifier = new FakeExtensionStatusNotifier();
        var viewModel = CreateMainWindowViewModel(extensionNotifier: notifier);
        notifier.Publish(ExtensionStatusCode.Warning, "GNOME extension requires logout/login to activate");

        var act = viewModel.Dispose;

        _ = act.Should().NotThrow();
    }

    [Fact]
    public void ExtensionStatusUpdated_WhenErrorPublishedAfterSubscription_ShowsErrorNotification()
    {
        var notifier = new FakeExtensionStatusNotifier();
        using var viewModel = CreateMainWindowViewModel(extensionNotifier: notifier);

        notifier.Publish(ExtensionStatusCode.Error, "Failed to install GNOME extension");

        _ = viewModel.HasExtensionWarning.Should().BeTrue();
        _ = viewModel.ExtensionWarning.Should().Be("Failed to install GNOME extension");
        _ = viewModel.IsAppNotificationVisible.Should().BeTrue();
        _ = viewModel.AppNotificationMessage.Should().Be("Failed to install GNOME extension");
        _ = viewModel.IsAppNotificationError.Should().BeTrue();
        _ = viewModel.IsAppNotificationWarning.Should().BeFalse();
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
            _triggerViewModel,
            _editorViewModel);
        var bottomItems = catalog.CreateBottomItems(_settingsViewModel);

        _ = topItems.Select(item => (item.LocalizationKey, item.Label, item.ViewModel)).Should().Equal(
            ("Navigation_Recording", "[Navigation_Recording]", _recordingViewModel),
            ("Navigation_Playback", "[Navigation_Playback]", _playbackViewModel),
            ("Navigation_Files", "[Navigation_Files]", _filesViewModel),
            ("Navigation_TextExpansion", "[Navigation_TextExpansion]", _textExpansionViewModel),
            ("Navigation_Shortcuts", "[Navigation_Shortcuts]", _shortcutViewModel),
            ("Navigation_Schedule", "[Navigation_Schedule]", _scheduleViewModel),
            ("Navigation_Triggers", "[Navigation_Triggers]", _triggerViewModel),
            ("Navigation_Editor", "[Navigation_Editor]", _editorViewModel));
        _ = topItems.Should().OnlyContain(item => Enum.IsDefined(item.Icon));

        _ = bottomItems.Select(item => (item.LocalizationKey, item.Label, item.ViewModel)).Should().Equal(
            ("Navigation_Settings", "[Navigation_Settings]", _settingsViewModel));
        _ = bottomItems.Should().OnlyContain(item => Enum.IsDefined(item.Icon));
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
            _triggerViewModel,
            _editorViewModel);
        var bottomItems = catalog.CreateBottomItems(_settingsViewModel);
        _ = _localizationService["Navigation_Recording"].Returns("[Navigation_Recording:updated]");
        _ = _localizationService["Navigation_Settings"].Returns("[Navigation_Settings:updated]");

        catalog.RefreshLabels(topItems, bottomItems);

        _ = topItems[0].Label.Should().Be("[Navigation_Recording:updated]");
        _ = bottomItems[0].Label.Should().Be("[Navigation_Settings:updated]");
    }

    [Fact]
    public async Task Construction_StartsOwnedShellInitializationTask()
    {
        var schedulerGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updateGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var schedulerService = Substitute.For<ISchedulerService>();
        _ = schedulerService.LoadAsync().Returns(async _ => await schedulerGate.Task);

        var updateService = Substitute.For<IUpdateService>();
        _ = updateService.CheckForUpdatesAsync().Returns(async unusedCallInfo =>
        {
            _ = await updateGate.Task;
            return new UpdateCheckResult
            {
                HasUpdate = true,
                LatestVersion = "9.9.9",
                ReleaseUrl = new Uri("https://example.invalid/releases/9.9.9"),
            };
        });

        var viewModel = CreateMainWindowViewModel(
            schedulerService: schedulerService,
            updateService: updateService,
            checkForUpdates: true);

        _ = viewModel.StartupInitializationTask.IsCompleted.Should().BeFalse();

        schedulerGate.SetResult(true);
        updateGate.SetResult(true);

        await viewModel.StartupInitializationTask;

        await schedulerService.Received(1).LoadAsync();
        _ = await updateService.Received(1).CheckForUpdatesAsync();
        _ = viewModel.LatestVersion.Should().Be("9.9.9");
        _ = viewModel.IsUpdateNotificationVisible.Should().BeTrue();
    }

    [Fact]
    public async Task Construction_WhenScheduleInitializationHandlesFailure_StartupTaskStillCompletesAndContinuesUpdateCheck()
    {
        var schedulerService = Substitute.For<ISchedulerService>();
        _ = schedulerService.LoadAsync().Returns(Task.FromException(new InvalidOperationException("scheduler boom")));

        var updateService = Substitute.For<IUpdateService>();
        _ = updateService.CheckForUpdatesAsync().Returns(Task.FromResult(new UpdateCheckResult
        {
            HasUpdate = true,
            LatestVersion = "1.2.3",
            ReleaseUrl = new Uri("https://example.invalid/releases/1.2.3"),
        }));

        var viewModel = CreateMainWindowViewModel(
            schedulerService: schedulerService,
            updateService: updateService,
            checkForUpdates: true);

        await viewModel.StartupInitializationTask;

        _ = schedulerService.Received(1).LoadAsync();
        _ = await updateService.Received(1).CheckForUpdatesAsync();
        _ = viewModel.LatestVersion.Should().Be("1.2.3");
        _ = viewModel.IsUpdateNotificationVisible.Should().BeTrue();
    }

    [Fact]
    public async Task RecordingStateChanged_UpdatesPlaybackAvailability()
    {
        await _recordingViewModel.StartRecordingAsync();

        _ = _playbackViewModel.CanPlayMacroExternal.Should().BeFalse();

        _ = _recordingViewModel.StopRecording();

        _ = _playbackViewModel.CanPlayMacroExternal.Should().BeTrue();
    }

    [Fact]
    public async Task PlaybackStateChanged_UpdatesRecordingAvailabilityAndFileManagement()
    {
        _filesViewModel.SetMacro(CreateMacro("Playback", EventType.MouseMove));
        var playbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowPlaybackCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(async unusedCallInfo =>
            {
                _ = playbackStarted.TrySetResult(true);
                _ = await allowPlaybackCompletion.Task;
            });

        var playTask = _playbackViewModel.PlayMacroAsync();
        _ = await playbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        _ = _recordingViewModel.CanStartRecordingExternal.Should().BeFalse();
        _ = _filesViewModel.CanManageLoadedMacrosExternal.Should().BeFalse();

        _playbackViewModel.StopPlayback();
        _ = allowPlaybackCompletion.TrySetResult(true);
        await playTask;

        _ = _recordingViewModel.CanStartRecordingExternal.Should().BeTrue();
        _ = _filesViewModel.CanManageLoadedMacrosExternal.Should().BeTrue();
    }

    [Fact]
    public async Task MacroLoaded_UpdatesGlobalStatusAndSharedMacroSelection()
    {
        var macro = new MacroSequence
        {
            Name = "TestMacro",
            Events = { new MacroEvent { Type = EventType.MouseMove } },
        };

        _ = _filesDialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/path/to/macro.macro"));
        _ = _fileManager.LoadAsync("/path/to/macro.macro")
            .Returns(Task.FromResult<MacroSequence?>(macro));

        await _filesViewModel.LoadMacroAsync();

        _ = _viewModel.GlobalStatus.Should().Be("[Status_LoadedMacro] TestMacro");
        _ = _filesViewModel.CurrentMacro.Should().BeSameAs(macro);
        _ = _playbackViewModel.HasMacro.Should().BeTrue();
        _ = _recordingViewModel.EventCount.Should().Be(1);
        _ = _recordingViewModel.RecordingStatus.Should().Be("[Recording_StatusLoadedEvents] 1");
    }

    [Fact]
    public void FilesSelectionChanged_WhenIdle_UpdatesRecordingCountersAndStatus()
    {
        var firstMacro = CreateMacro("First", EventType.MouseMove, EventType.KeyPress);
        var secondMacro = CreateMacro("Second", EventType.ButtonPress, EventType.ButtonRelease, EventType.MouseMove, EventType.KeyPress);

        _filesViewModel.SetMacro(firstMacro);
        _filesViewModel.SetMacro(secondMacro);

        _filesViewModel.SelectedMacroItem = _filesViewModel.LoadedMacros[0];
        _ = _recordingViewModel.EventCount.Should().Be(2);
        _ = _recordingViewModel.MouseEventCount.Should().Be(1);
        _ = _recordingViewModel.KeyboardEventCount.Should().Be(1);
        _ = _recordingViewModel.RecordingStatus.Should().Be("[Recording_StatusLoadedEvents] 2");

        _filesViewModel.SelectedMacroItem = _filesViewModel.LoadedMacros[1];
        _ = _recordingViewModel.EventCount.Should().Be(4);
        _ = _recordingViewModel.MouseEventCount.Should().Be(3);
        _ = _recordingViewModel.KeyboardEventCount.Should().Be(1);
        _ = _recordingViewModel.RecordingStatus.Should().Be("[Recording_StatusLoadedEvents] 4");
    }

    [Fact]
    public async Task FilesSelectionChanged_WhenRecordingActive_DoesNotOverwriteRecordingCounters()
    {
        var firstMacro = CreateMacro("First", EventType.MouseMove, EventType.KeyPress, EventType.KeyRelease);
        var secondMacro = CreateMacro("Second", EventType.ButtonPress, EventType.ButtonRelease, EventType.MouseMove, EventType.KeyPress);

        _filesViewModel.SetMacro(firstMacro);
        _filesViewModel.SetMacro(secondMacro);

        var firstItem = _filesViewModel.LoadedMacros[0];
        var secondItem = _filesViewModel.LoadedMacros[1];
        _filesViewModel.SelectedMacroItem = firstItem;

        await _recordingViewModel.StartRecordingAsync();
        PublishRecordedEvents(EventType.MouseMove, EventType.ButtonPress, EventType.ButtonRelease, EventType.KeyPress, EventType.KeyPress, EventType.KeyPress, EventType.KeyRelease);

        _filesViewModel.SelectedMacroItem = secondItem;

        _ = _recordingViewModel.EventCount.Should().Be(7);
        _ = _recordingViewModel.MouseEventCount.Should().Be(3);
        _ = _recordingViewModel.KeyboardEventCount.Should().Be(4);
        _ = _recordingViewModel.StopRecording();
    }

    [Fact]
    public async Task RenameSelectedLoadedMacro_DoesNotRewriteRecordingStatusOrCounters()
    {
        var macro = CreateMacro("Original", Enumerable.Repeat(EventType.MouseMove, 40).Concat(Enumerable.Repeat(EventType.KeyPress, 59)).ToArray());
        _ = _recorder.StopRecording().Returns(macro);
        await _recordingViewModel.StartRecordingAsync();
        PublishRecordedEvents(Enumerable.Repeat(EventType.MouseMove, 40).Concat(Enumerable.Repeat(EventType.KeyPress, 59)));
        _ = _recordingViewModel.StopRecording();

        _filesViewModel.MacroName = "Renamed Macro";

        _ = _filesViewModel.SelectedMacroItem!.Name.Should().Be("Renamed Macro");
        _ = _recordingViewModel.RecordingStatus.Should().Be("[Recording_StatusRecordedEvents] 99");
        _ = _recordingViewModel.EventCount.Should().Be(99);
        _ = _recordingViewModel.MouseEventCount.Should().Be(40);
        _ = _recordingViewModel.KeyboardEventCount.Should().Be(59);
    }

    [Fact]
    public async Task RecordingCompleted_WhenMacroIsAutoSelected_PreservesRecordedStatus()
    {
        var recordedMacro = CreateMacro("RecordedMacro", EventType.MouseMove, EventType.KeyPress);
        _ = _recorder.StopRecording().Returns(recordedMacro);
        await _recordingViewModel.StartRecordingAsync();
        PublishRecordedEvents(EventType.MouseMove, EventType.KeyPress);

        var result = _recordingViewModel.StopRecording();

        _ = result.Should().BeSameAs(recordedMacro);
        _ = _filesViewModel.CurrentMacro.Should().BeSameAs(recordedMacro);
        _ = _recordingViewModel.RecordingStatus.Should().Be("[Recording_StatusRecordedEvents] 2");
    }

    [Fact]
    public async Task RemovingLastLoadedMacro_ClearsRecordingCounters()
    {
        var macro = CreateMacro("LoadedMacro", EventType.MouseMove, EventType.ButtonPress, EventType.KeyPress);
        _filesViewModel.SetMacro(macro);
        _ = _filesDialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        _ = _recordingViewModel.EventCount.Should().Be(3);
        _ = _recordingViewModel.MouseEventCount.Should().Be(2);
        _ = _recordingViewModel.KeyboardEventCount.Should().Be(1);

        await _filesViewModel.RemoveLoadedMacroCommand.ExecuteAsync(_filesViewModel.SelectedMacroItem);

        _ = _recordingViewModel.EventCount.Should().Be(0);
        _ = _recordingViewModel.MouseEventCount.Should().Be(0);
        _ = _recordingViewModel.KeyboardEventCount.Should().Be(0);
        _ = _recordingViewModel.RecordingStatus.Should().Be("[Recording_StatusReady]");
    }

    [Fact]
    public async Task EditorMacroCreated_WhenSavingDifferentEditorDocument_DoesNotOverwriteSelectedLoadedMacro()
    {
        var editorMacro = CreateMacro("Editor Macro", EventType.MouseMove, EventType.KeyPress);
        var editorMacroUpdated = CreateMacro("Editor Macro Updated", EventType.ButtonPress, EventType.KeyPress);
        var unrelatedSelectedMacro = CreateMacro("Selected Macro", EventType.ButtonRelease, EventType.KeyRelease);

        await SaveEditorMacroAsync(editorMacro);
        var trackedEditorItem = _filesViewModel.SelectedMacroItem;
        _ = trackedEditorItem.Should().NotBeNull();

        _filesViewModel.SetMacro(unrelatedSelectedMacro);
        var selectedItem = _filesViewModel.SelectedMacroItem;
        _ = selectedItem.Should().NotBeNull();
        _ = selectedItem.Should().NotBeSameAs(trackedEditorItem);

        await SaveEditorMacroAsync(editorMacroUpdated);

        _ = _filesViewModel.LoadedMacros.Should().HaveCount(2);
        _ = trackedEditorItem!.Macro.Should().BeSameAs(editorMacroUpdated);
        _ = trackedEditorItem.Name.Should().Be("Editor Macro Updated");
        _ = selectedItem!.Macro.Should().BeSameAs(unrelatedSelectedMacro);
        _ = selectedItem.Name.Should().Be("Selected Macro");
        _ = _filesViewModel.SelectedMacroItem.Should().BeSameAs(selectedItem);
    }

    [Fact]
    public async Task EditorMacroCreated_WhenSavingSameMacroAgain_DoesNotAppendDuplicateLoadedItem()
    {
        var firstMacro = CreateMacro("Editor Macro", EventType.MouseMove, EventType.KeyPress);
        var updatedMacro = CreateMacro("Editor Macro Updated", EventType.ButtonPress, EventType.ButtonRelease, EventType.KeyPress);

        await SaveEditorMacroAsync(firstMacro);
        var selectedItem = _filesViewModel.SelectedMacroItem;
        _ = selectedItem.Should().NotBeNull();
        selectedItem!.SequenceRepeatCount = 4;

        await SaveEditorMacroAsync(updatedMacro);

        _ = _filesViewModel.LoadedMacros.Should().ContainSingle();
        _ = _filesViewModel.SelectedMacroItem.Should().BeSameAs(selectedItem);
        _ = _filesViewModel.SelectedMacroItem!.Macro.Should().BeSameAs(updatedMacro);
        _ = _filesViewModel.SelectedMacroItem.Name.Should().Be("Editor Macro Updated");
        _ = _filesViewModel.SelectedMacroItem.SequenceRepeatCount.Should().Be(4);
    }

    [Fact]
    public async Task EditorMacroCreated_WhenSavePathChanges_UpdatesLoadedMacroSourcePath()
    {
        var firstMacro = CreateMacro("Editor Macro", EventType.MouseMove, EventType.KeyPress);
        var updatedMacro = CreateMacro("Editor Macro Updated", EventType.ButtonPress, EventType.KeyPress);

        await SaveEditorMacroAsync(firstMacro, "/tmp/editor-original.macro");
        var item = _filesViewModel.SelectedMacroItem;

        _ = item.Should().NotBeNull();
        _ = item!.SourcePath.Should().Be("/tmp/editor-original.macro");

        await SaveEditorMacroAsync(updatedMacro, "/tmp/editor-save-as.macro");

        _ = _filesViewModel.LoadedMacros.Should().ContainSingle();
        _ = item.Macro.Should().BeSameAs(updatedMacro);
        _ = item.SourcePath.Should().Be("/tmp/editor-save-as.macro");
        _ = item.Description.Should().Contain("editor-save-as.macro");
    }

    [Fact]
    public async Task EditorMacroCreated_WhenMacroHasOnlyScreenReadingScriptSteps_ReportsActionCount()
    {
        var macro = new MacroSequence
        {
            Name = "Screen Reading Macro",
            ScriptSteps =
            {
                "pixelcolor 10 20 color",
                "waitcolor 11 22 00FFAA 2500",
                "pixelsearch 0 0 3 3 123456 x y",
            },
        };

        await SaveEditorMacroAsync(macro);

        _ = _viewModel.GlobalStatus.Should().Be("[Status_CreatedMacro] Screen Reading Macro (3)");
        _ = _filesViewModel.SelectedMacroItem!.EventCount.Should().Be(3);
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
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(async unusedCallInfo =>
            {
                _ = playStarted.TrySetResult(true);
                _ = await allowCompletion.Task;
            });

        var playTask = _playbackViewModel.PlayMacroAsync();
        _ = await playStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        _ = _filesViewModel.CanManageLoadedMacrosExternal.Should().BeFalse();

        _playbackViewModel.StopPlayback();

        _ = _filesViewModel.CanManageLoadedMacrosExternal.Should().BeFalse();

        _ = allowCompletion.TrySetResult(true);
        await playTask;

        _ = _filesViewModel.CanManageLoadedMacrosExternal.Should().BeTrue();
    }

    [Fact]
    public async Task StopPlayback_WhenCompletionOccursOffThread_ReenablesRecordingAndFilesOnUiExecutorAfterCleanup()
    {
        var macro = CreateMacro("Playback", EventType.MouseMove);
        _filesViewModel.SetMacro(macro);
        _playbackViewModel.CanPlayMacroExternal = true;

        var playStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var playbackCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowPlayerReturn = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(async unusedCallInfo =>
            {
                playStarted.SetResult(true);
                _ = await playbackCompleted.Task.ConfigureAwait(false);
                cleanupStarted.SetResult(true);
                _ = await allowPlayerReturn.Task.ConfigureAwait(false);
            });

        var uiExecutor = new DeferredUiExecutor();
        var availabilityChanges = new List<(string PropertyName, bool Value, SynchronizationContext? Context)>();
        _recordingViewModel.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(RecordingViewModel.CanStartRecordingExternal), StringComparison.Ordinal))
            {
                availabilityChanges.Add((args.PropertyName, _recordingViewModel.CanStartRecordingExternal, SynchronizationContext.Current));
            }
        };
        _filesViewModel.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(FilesViewModel.CanManageLoadedMacrosExternal), StringComparison.Ordinal))
            {
                availabilityChanges.Add((args.PropertyName, _filesViewModel.CanManageLoadedMacrosExternal, SynchronizationContext.Current));
            }
        };

        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(uiExecutor);
        Task playTask;
        try
        {
            playTask = _playbackViewModel.PlayMacroAsync();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        _ = await playStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        _ = _recordingViewModel.CanStartRecordingExternal.Should().BeFalse();
        _ = _filesViewModel.CanManageLoadedMacrosExternal.Should().BeFalse();
        availabilityChanges.Clear();

        _playbackViewModel.StopPlayback();
        _ = await Task.Run(() => playbackCompleted.TrySetResult(true));
        _ = await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        _ = _recordingViewModel.CanStartRecordingExternal.Should().BeFalse();
        _ = _filesViewModel.CanManageLoadedMacrosExternal.Should().BeFalse();

        _ = allowPlayerReturn.TrySetResult(true);
        var firstCompleted = await Task.WhenAny(playTask, uiExecutor.PostObserved.Task);

        _ = firstCompleted.Should().BeSameAs(uiExecutor.PostObserved.Task);
        _ = _recordingViewModel.CanStartRecordingExternal.Should().BeFalse();
        _ = _filesViewModel.CanManageLoadedMacrosExternal.Should().BeFalse();

        uiExecutor.RunAll();
        await playTask;

        _ = _recordingViewModel.CanStartRecordingExternal.Should().BeTrue();
        _ = _filesViewModel.CanManageLoadedMacrosExternal.Should().BeTrue();
        _ = availabilityChanges.Select(change => (change.PropertyName, change.Value)).Should().Equal(
            (nameof(RecordingViewModel.CanStartRecordingExternal), true),
            (nameof(FilesViewModel.CanManageLoadedMacrosExternal), true));
        _ = availabilityChanges.Should().OnlyContain(change => ReferenceEquals(change.Context, uiExecutor));
    }

    [Fact]
    public void DismissUpdateNotification_HidesNotification()
    {
        _viewModel.IsUpdateNotificationVisible = true;

        _viewModel.DismissUpdateNotification();

        _ = _viewModel.IsUpdateNotificationVisible.Should().BeFalse();
    }

    [Fact]
    public void OpenUpdateUrl_AlwaysDismissesNotification()
    {
        _viewModel.IsUpdateNotificationVisible = true;

        _viewModel.OpenUpdateUrl();

        _ = _viewModel.IsUpdateNotificationVisible.Should().BeFalse();
    }

    [Fact]
    public void DismissUpdateNotificationCommand_ExecutesBoundDismissAction()
    {
        _viewModel.IsUpdateNotificationVisible = true;

        _viewModel.DismissUpdateNotificationCommand.Execute(parameter: null);

        _ = _viewModel.IsUpdateNotificationVisible.Should().BeFalse();
    }

    [Fact]
    public async Task OpenUpdateUrlCommand_ExecutesBoundOpenActionAndDismissesNotification()
    {
        var updateService = Substitute.For<IUpdateService>();
        _ = updateService.CheckForUpdatesAsync().Returns(Task.FromResult(new UpdateCheckResult
        {
            HasUpdate = true,
            LatestVersion = "9.9.9",
            ReleaseUrl = new Uri("https://example.invalid/releases/latest", UriKind.Absolute),
        }));
        var opened = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var externalUrlOpener = Substitute.For<IExternalUrlOpener>();
        _ = externalUrlOpener.OpenAsync(Arg.Any<Uri>()).Returns(callInfo =>
        {
            _ = opened.TrySetResult(callInfo.Arg<Uri>());
            return Task.CompletedTask;
        });
        using var viewModel = CreateMainWindowViewModel(
            updateService: updateService,
            checkForUpdates: true,
            externalUrlOpener: externalUrlOpener);
        await viewModel.StartupInitializationTask;

        viewModel.OpenUpdateUrlCommand.Execute(parameter: null);

        _ = (await opened.Task.WaitAsync(TimeSpan.FromSeconds(2))).Should().Be(new Uri("https://example.invalid/releases/latest", UriKind.Absolute));
        await externalUrlOpener.Received(1).OpenAsync(new Uri("https://example.invalid/releases/latest", UriKind.Absolute));
        _ = viewModel.IsUpdateNotificationVisible.Should().BeFalse();
    }

    [Fact]
    public async Task StartupInitialization_WhenPlatformStartupNotificationAvailable_ShowsDismissibleWarningNotification()
    {
        var platformNotificationProvider = Substitute.For<IPlatformStartupNotificationProvider>();
        _ = platformNotificationProvider.GetStartupNotification().Returns(new PlatformStartupNotification(
            "Platform Compatibility",
            "Platform startup warning is active.",
            PlatformStartupNotificationSeverity.Warning));

        using var viewModel = CreateMainWindowViewModel(platformStartupNotificationProviders: [platformNotificationProvider]);

        await viewModel.StartupInitializationTask;

        _ = viewModel.IsAppNotificationVisible.Should().BeTrue();
        _ = viewModel.AppNotificationTitle.Should().Be("Platform Compatibility");
        _ = viewModel.AppNotificationMessage.Should().Be("Platform startup warning is active.");
        _ = viewModel.IsAppNotificationWarning.Should().BeTrue();

        viewModel.DismissAppNotification();

        _ = viewModel.IsAppNotificationVisible.Should().BeFalse();
    }

    [Fact]
    public async Task StartupInitialization_WhenExtensionWarningAlreadyVisible_DoesNotReplaceItWithPlatformNotification()
    {
        var notifier = new FakeExtensionStatusNotifier();
        notifier.Publish(ExtensionStatusCode.Warning, "GNOME extension requires logout/login to activate");
        var platformNotificationProvider = Substitute.For<IPlatformStartupNotificationProvider>();
        _ = platformNotificationProvider.GetStartupNotification().Returns(new PlatformStartupNotification(
            "Platform Compatibility",
            "Platform startup warning is active.",
            PlatformStartupNotificationSeverity.Warning));

        using var viewModel = CreateMainWindowViewModel(
            extensionNotifier: notifier,
            platformStartupNotificationProviders: [platformNotificationProvider]);

        await viewModel.StartupInitializationTask;

        _ = viewModel.HasExtensionWarning.Should().BeTrue();
        _ = viewModel.ExtensionWarning.Should().Be("GNOME extension requires logout/login to activate");
        _ = viewModel.AppNotificationTitle.Should().Be("[MainWindow_GnomeExtensionTitle]");
        _ = viewModel.AppNotificationMessage.Should().Be("GNOME extension requires logout/login to activate");
        _ = viewModel.IsAppNotificationWarning.Should().BeTrue();
        _ = platformNotificationProvider.DidNotReceive().GetStartupNotification();
    }

    [Fact]
    public async Task StartupInitialization_WhenPlatformStartupNotificationProviderThrows_SkipsProvider()
    {
        var throwingProvider = Substitute.For<IPlatformStartupNotificationProvider>();
        _ = throwingProvider.GetStartupNotification().Returns(_ => throw new InvalidOperationException("provider failed"));
        var workingProvider = Substitute.For<IPlatformStartupNotificationProvider>();
        _ = workingProvider.GetStartupNotification().Returns(new PlatformStartupNotification(
            "Platform Compatibility",
            "Platform startup warning is active.",
            PlatformStartupNotificationSeverity.Warning));

        using var viewModel = CreateMainWindowViewModel(
            platformStartupNotificationProviders: [throwingProvider, workingProvider]);

        await viewModel.StartupInitializationTask;

        _ = viewModel.IsAppNotificationVisible.Should().BeTrue();
        _ = viewModel.AppNotificationTitle.Should().Be("Platform Compatibility");
        _ = viewModel.AppNotificationMessage.Should().Be("Platform startup warning is active.");
    }

    [Fact]
    public void CultureChanged_RefreshesNavigationLabels_ByLocalizationKey()
    {
        _ = _viewModel.TopNavigationItems[0].LocalizationKey.Should().Be("Navigation_Recording");
        _ = _viewModel.BottomNavigationItems[0].LocalizationKey.Should().Be("Navigation_Settings");

        _ = _localizationService["Navigation_Recording"].Returns("[Navigation_Recording:updated]");
        _ = _localizationService["Navigation_Settings"].Returns("[Navigation_Settings:updated]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _ = _viewModel.TopNavigationItems[0].Label.Should().Be("[Navigation_Recording:updated]");
        _ = _viewModel.BottomNavigationItems[0].Label.Should().Be("[Navigation_Settings:updated]");
    }

    [Fact]
    public void CultureChanged_WhenIdleAndNoMacro_RefreshesGlobalReadyStatus()
    {
        _viewModel.GlobalStatus = "[Status_Ready]";
        _ = _localizationService["Status_Ready"].Returns("[Status_Ready:updated]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _ = _viewModel.GlobalStatus.Should().Be("[Status_Ready:updated]");
    }

    [Fact]
    public void CultureChanged_WhenIdleWithMacro_RefreshesGlobalStatusFromRecordingSummary()
    {
        var macro = CreateMacro("Macro", EventType.MouseMove, EventType.KeyPress);
        _filesViewModel.SetMacro(macro);
        _ = _localizationService["Recording_StatusLoadedEvents"].Returns("[Recording_StatusLoadedEvents:updated] {0}");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _ = _viewModel.GlobalStatus.Should().Be("[Recording_StatusLoadedEvents:updated] 2");
    }

    [Theory]
    [InlineData(DisplayEnvironment.LinuxX11)]
    [InlineData(DisplayEnvironment.LinuxWayland)]
    [InlineData(DisplayEnvironment.LinuxHyprland)]
    [InlineData(DisplayEnvironment.LinuxWayfire)]
    [InlineData(DisplayEnvironment.LinuxKDE)]
    [InlineData(DisplayEnvironment.LinuxGnome)]
    public void GetBackendTroubleshootingHintKey_WhenLinuxEnvironment_ReturnsSystemctlGuidanceKey(DisplayEnvironment environment)
    {
        var hint = MainWindowViewModel.GetBackendTroubleshootingHintKey(environment);

        _ = hint.Should().NotBeNull();
        _ = hint.Should().Be("MainWindow_BackendTroubleshootingLinux");
    }

    [Theory]
    [InlineData(DisplayEnvironment.Windows)]
    [InlineData(DisplayEnvironment.MacOS)]
    public void GetBackendTroubleshootingHintKey_WhenNonLinuxEnvironment_ReturnsPlatformKey(DisplayEnvironment environment)
    {
        var hint = MainWindowViewModel.GetBackendTroubleshootingHintKey(environment);

        _ = hint.Should().NotBeNull();
        _ = hint.Should().NotBe("MainWindow_BackendTroubleshootingLinux");
    }

    [Fact]
    public void GetBackendTroubleshootingHintKey_WhenUnknownEnvironment_ReturnsNull()
    {
        var hint = MainWindowViewModel.GetBackendTroubleshootingHintKey(DisplayEnvironment.Unknown);

        _ = hint.Should().BeNull();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var act = () =>
        {
            _viewModel.Dispose();
            _viewModel.Dispose();
        };

        _ = act.Should().NotThrow();
    }

    private static MainWindowViewModel CreateMainWindowViewModel(
        ISchedulerService? schedulerService = null,
        IUpdateService? updateService = null,
        bool? checkForUpdates = null,
        IExtensionStatusNotifier? extensionNotifier = null,
        IEnumerable<IPlatformStartupNotificationProvider>? platformStartupNotificationProviders = null,
        IExternalUrlOpener? externalUrlOpener = null)
    {
        var settingsService = Substitute.For<ISettingsService>();
        _ = settingsService.Current.Returns(new AppSettings
        {
            CheckForUpdates = checkForUpdates ?? false,
        });
        var runtimeContext = Substitute.For<IRuntimeContext>();
        _ = runtimeContext.IsLinux.Returns(returnThis: true);

        var localizationService = Substitute.For<ILocalizationService>();
        _ = localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _ = localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
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
            "MainWindow_GnomeExtensionTitle" => "[MainWindow_GnomeExtensionTitle]",
            "MainWindow_BackendErrorTitle" => "[MainWindow_BackendErrorTitle]",
            "MainWindow_BackendTroubleshootingFormat" => "Troubleshooting: {0}",
            "MainWindow_BackendTroubleshootingLinux" => "check `systemctl status crossmacro.service`; direct device mode may require Linux input permissions instead.",
            "MainWindow_BackendTroubleshootingWindows" => "restart CrossMacro and verify the background service is running.",
            "MainWindow_BackendTroubleshootingMacOS" => "restart CrossMacro and verify Input Monitoring and Accessibility permissions in System Settings.",
            "Navigation_Recording" => "[Navigation_Recording]",
            "Navigation_Playback" => "[Navigation_Playback]",
            "Navigation_Files" => "[Navigation_Files]",
            "Navigation_TextExpansion" => "[Navigation_TextExpansion]",
            "Navigation_Shortcuts" => "[Navigation_Shortcuts]",
            "Navigation_Schedule" => "[Navigation_Schedule]",
            "Navigation_Triggers" => "[Navigation_Triggers]",
            "Navigation_Editor" => "[Navigation_Editor]",
            "Navigation_Settings" => "[Navigation_Settings]",
            _ => call.Arg<string>(),
        });

        var hotkeyService = Substitute.For<IGlobalHotkeyService>();
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var loadedMacroSession = new LoadedMacroSession(localizationService);
        var recorder = Substitute.For<IMacroRecorder>();
        var recordingViewModel = new RecordingViewModel(recorder, hotkeyService, settingsService, localizationService, runtimeContext);

        var player = Substitute.For<IMacroPlayer>();
        _ = player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var playbackViewModel = new PlaybackViewModel(player, settingsService, loadedMacroSession);

        var fileManager = Substitute.For<IMacroFileManager>();
        var filesDialogService = Substitute.For<IDialogService>();
        externalUrlOpener ??= Substitute.For<IExternalUrlOpener>();
        var filesViewModel = new FilesViewModel(fileManager, filesDialogService, loadedMacroSession, localizationService);

        var textExpansionStorage = Substitute.For<ITextExpansionStore>();
        var dialogService = Substitute.For<IDialogService>();
        var environmentInfo = Substitute.For<IEnvironmentInfoProvider>();
        _ = environmentInfo.WindowManagerHandlesCloseButton.Returns(returnThis: false);
        _ = environmentInfo.CurrentEnvironment.Returns(DisplayEnvironment.Windows);
        var textExpansionViewModel = new TextExpansionViewModel(textExpansionStorage, dialogService, environmentInfo, localizationService);

        if (schedulerService is null)
        {
            schedulerService = Substitute.For<ISchedulerService>();
            _ = schedulerService.LoadAsync().Returns(Task.CompletedTask);
        }

        var timeProvider = Substitute.For<TimeProvider>();
        _ = timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 1, 1, 7, 0, 0, TimeSpan.Zero));
        var scheduleViewModel = new ScheduleViewModel(schedulerService, dialogService, timeProvider, localizationService);

        var shortcutService = Substitute.For<IShortcutService>();
        var shortcutViewModel = new ShortcutViewModel(shortcutService, dialogService, hotkeyService, localizationService);

        var triggerService = Substitute.For<ITriggerService>();
        _ = triggerService.Tasks.Returns(new System.Collections.ObjectModel.ObservableCollection<TriggerTask>());
        _ = triggerService.LoadAsync().Returns(Task.CompletedTask);
        var triggerViewModel = new TriggerViewModel(triggerService, profileManager: null, dialogService, localizationService, windowManager: null);

        var hotkeySettings = new HotkeySettings();
        var textExpansionService = Substitute.For<ITextExpansionService>();
        var runtimeLogLevelService = Substitute.For<IRuntimeLogLevelService>();
        var themeService = Substitute.For<IThemeService>();
        _ = themeService.AvailableThemes.Returns(["Classic"]);
        _ = themeService.CurrentTheme.Returns("Classic");
        _ = themeService
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
            Substitute.For<IRuntimeContext>(),
            localizationService);

        var editorConverter = Substitute.For<IEditorActionConverter>();
        var editorValidator = Substitute.For<IEditorActionValidator>();
        var captureService = Substitute.For<ICoordinateCaptureService>();
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        var editorViewModel = new EditorViewModel(editorConverter, editorValidator, captureService, fileManager, dialogService, keyCodeMapper, Substitute.For<CrossMacro.Core.Services.IMacroPlayer>());

        return new MainWindowViewModel(
            recordingViewModel,
            playbackViewModel,
            filesViewModel,
            textExpansionViewModel,
            scheduleViewModel,
            shortcutViewModel,
            triggerViewModel,
            settingsViewModel,
            editorViewModel,
            hotkeyService,
            positionProvider,
            environmentInfo,
            externalUrlOpener,
            localizationService,
            extensionNotifier,
            updateService,
            platformStartupNotificationProviders);
    }

    private sealed class FakeExtensionStatusNotifier : IExtensionStatusNotifier
    {
        public event EventHandler<ExtensionStatusChangedEventArgs>? ExtensionStatusUpdated;
        public event EventHandler<ExtensionStatusMessageEventArgs>? ExtensionStatusChanged;

        public ExtensionStatusChangedEventArgs? CurrentExtensionStatus { get; private set; }

        public void Publish(ExtensionStatusCode code, string message)
        {
            var args = new ExtensionStatusChangedEventArgs(code, message);
            CurrentExtensionStatus = args;
            ExtensionStatusUpdated?.Invoke(this, args);
            ExtensionStatusChanged?.Invoke(this, new ExtensionStatusMessageEventArgs(message));
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

    private void PublishRecordedEvents(IEnumerable<EventType> eventTypes)
    {
        foreach (var eventType in eventTypes)
        {
            _recorder.EventRecorded += Raise.Event<EventHandler<MacroEventRecordedEventArgs>>(
                _recorder,
                new MacroEventRecordedEventArgs(new MacroEvent { Type = eventType }));
        }
    }

    private void PublishRecordedEvents(params EventType[] eventTypes)
    {
        PublishRecordedEvents((IEnumerable<EventType>)eventTypes);
    }

    private async Task SaveEditorMacroAsync(MacroSequence macro, string sourcePath = "/tmp/editor-test.macro")
    {
        if (_editorViewModel.Actions.Count is 0)
        {
            _editorViewModel.AddAction();
        }

        _ = _editorValidator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>())
            .Returns((true, new List<string>()));
        _ = _editorConverter.ToMacroSequence(Arg.Any<EditorMacroProjection>()).Returns(macro);
        _ = _editorDialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(sourcePath);
        _ = _fileManager.SaveAsync(macro, sourcePath).Returns(Task.CompletedTask);

        await _editorViewModel.SaveMacroAsync();
    }

}
