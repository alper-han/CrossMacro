namespace CrossMacro.UI.Tests.ViewModels;

/// <summary>
/// Builds a <see cref="MainWindowViewModel"/> together with the nine child view models it is wired from.
/// The shell does not own its children, so the harness owns and disposes all of them.
/// </summary>
internal sealed class MainWindowHarness : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> LocalizationOverrides = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Files_LoadedMacroDescription"] = "[Files_LoadedMacroDescription] {0} | {1}",
        ["Status_CreatedMacro"] = "[Status_CreatedMacro] {0} ({1})",
        ["MainWindow_UpdateAvailableVersion"] = "v{0} is available",
        ["MainWindow_BackendTroubleshootingFormat"] = "Troubleshooting: {0}",
        ["MainWindow_BackendTroubleshootingLinux"] = "check `systemctl status crossmacro.service`; direct device mode may require Linux input permissions instead.",
        ["MainWindow_BackendTroubleshootingWindows"] = "restart CrossMacro and verify the background service is running.",
        ["MainWindow_BackendTroubleshootingMacOS"] = "restart CrossMacro and verify Input Monitoring and Accessibility permissions in System Settings.",
    };

    private MainWindowHarness()
    {
    }

    public IMacroRecorder Recorder { get; private init; } = null!;

    public IMacroPlayer Player { get; private init; } = null!;

    public IMacroFileManager FileManager { get; private init; } = null!;

    public ISettingsService SettingsService { get; private init; } = null!;

    public IGlobalHotkeyService HotkeyService { get; private init; } = null!;

    public IDialogService FilesDialogService { get; private init; } = null!;

    public IDialogService EditorDialogService { get; private init; } = null!;

    public IExternalUrlOpener ExternalUrlOpener { get; private init; } = null!;

    public ISchedulerService SchedulerService { get; private init; } = null!;

    public IShortcutService ShortcutService { get; private init; } = null!;

    public ILocalizationService LocalizationService { get; private init; } = null!;

    public LoadedMacroSession LoadedMacroSession { get; private init; } = null!;

    public IEditorActionConverter EditorConverter { get; private init; } = null!;

    public IEditorActionValidator EditorValidator { get; private init; } = null!;

    /// <summary>Routes the playback view model's UI work; immediate unless a test retargets it.</summary>
    public SwitchingUiDispatcher UiDispatcher { get; private init; } = null!;

    public RecordingViewModel Recording { get; private init; } = null!;

    public PlaybackViewModel Playback { get; private init; } = null!;

    public FilesViewModel Files { get; private init; } = null!;

    public TextExpansionViewModel TextExpansion { get; private init; } = null!;

    public ScheduleViewModel Schedule { get; private init; } = null!;

    public ShortcutViewModel Shortcut { get; private init; } = null!;

    public TriggerViewModel Trigger { get; private init; } = null!;

    public SettingsViewModel Settings { get; private init; } = null!;

    public EditorViewModel Editor { get; private init; } = null!;

    public MainWindowViewModel ViewModel { get; private init; } = null!;

    public static MainWindowHarness Create(
        ISchedulerService? schedulerService = null,
        IUpdateService? updateService = null,
        bool checkForUpdates = false,
        IExtensionStatusNotifier? extensionNotifier = null,
        IEnumerable<IPlatformStartupNotificationProvider>? platformStartupNotificationProviders = null,
        IExternalUrlOpener? externalUrlOpener = null,
        IMacroPlayer? macroPlayer = null,
        ISettingsService? settingsService = null)
    {
        if (settingsService is null)
        {
            settingsService = SettingsServiceSubstitute.Create();
            _ = settingsService.Current.Returns(new AppSettings { CheckForUpdates = checkForUpdates });
        }

        var localizationService = TestLocalization.Bracketed(LocalizationOverrides, culture: CultureInfo.GetCultureInfo("en"));
        var uiDispatcher = new SwitchingUiDispatcher();
        var hotkeyService = Substitute.For<IGlobalHotkeyService>();
        var loadedMacroSession = new LoadedMacroSession(localizationService);

        var recorder = Substitute.For<IMacroRecorder>();
        var recording = new RecordingViewModel(
            recorder,
            hotkeyService,
            settingsService,
            localizationService,
            FakeRuntimeContext.Linux(),
            new SettingsChangeCoordinator(settingsService),
            uiDispatcher: ImmediateUiDispatcher.Instance);

        var player = macroPlayer ?? Substitute.For<IMacroPlayer>();
        if (macroPlayer is null)
        {
            _ = player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
        }

        var playback = new PlaybackViewModel(
            player,
            settingsService,
            loadedMacroSession,
            localizationService,
            Substitute.For<IDialogService>(),
            new SettingsChangeCoordinator(settingsService),
            uiDispatcher: uiDispatcher);

        var fileManager = Substitute.For<IMacroFileManager>();
        var filesDialogService = Substitute.For<IDialogService>();
        externalUrlOpener ??= Substitute.For<IExternalUrlOpener>();
        var files = new FilesViewModel(fileManager, filesDialogService, loadedMacroSession, localizationService, uiDispatcher: ImmediateUiDispatcher.Instance);

        var dialogService = Substitute.For<IDialogService>();
        var environmentInfo = Substitute.For<IEnvironmentInfoProvider>();
        _ = environmentInfo.WindowManagerHandlesCloseButton.Returns(returnThis: false);
        _ = environmentInfo.CurrentEnvironment.Returns(DisplayEnvironment.Windows);

        var textExpansion = new TextExpansionViewModel(
            UiAutomationTestComposition.Create(Substitute.For<ITextExpansionStore>()),
            dialogService,
            environmentInfo,
            localizationService,
            uiDispatcher: ImmediateUiDispatcher.Instance);

        if (schedulerService is null)
        {
            schedulerService = Substitute.For<ISchedulerService>();
            _ = schedulerService.Tasks.Returns(new ObservableCollection<ScheduledTask>());
            _ = schedulerService.LoadAsync().Returns(Task.CompletedTask);
        }

        var timeProvider = Substitute.For<TimeProvider>();
        _ = timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 1, 1, 7, 0, 0, TimeSpan.Zero));
        var schedule = new ScheduleViewModel(
            UiAutomationTestComposition.Create(schedulerService),
            schedulerService,
            dialogService,
            timeProvider,
            localizationService,
            uiDispatcher: ImmediateUiDispatcher.Instance);

        var shortcutService = Substitute.For<IShortcutService>();
        _ = shortcutService.Tasks.Returns(new ObservableCollection<ShortcutTask>());
        var shortcut = new ShortcutViewModel(
            UiAutomationTestComposition.Create(shortcutService),
            shortcutService,
            dialogService,
            hotkeyService,
            localizationService,
            new AcceptingShortcutHotkeyNormalizer(),
            uiDispatcher: ImmediateUiDispatcher.Instance);

        var triggerService = Substitute.For<ITriggerService>();
        _ = triggerService.Tasks.Returns(new ObservableCollection<TriggerTask>());
        _ = triggerService.LoadAsync().Returns(Task.CompletedTask);
        var trigger = new TriggerViewModel(
            UiAutomationTestComposition.Create(triggerService),
            triggerService,
            profileManager: null,
            dialogService,
            localizationService,
            windowManager: null,
            uiDispatcher: ImmediateUiDispatcher.Instance);

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
        var settings = new SettingsViewModel(
            hotkeyService,
            settingsService,
            Substitute.For<ITextExpansionService>(),
            new HotkeySettings(),
            externalUrlOpener,
            Substitute.For<IRuntimeLogLevelService>(),
            themeService,
            Substitute.For<IRuntimeContext>(),
            localizationService,
            new SettingsChangeCoordinator(settingsService),
            uiDispatcher: ImmediateUiDispatcher.Instance);

        var editorConverter = Substitute.For<IEditorActionConverter>();
        var editorValidator = Substitute.For<IEditorActionValidator>();
        var editorFactory = new EditorDocumentFactory(
            editorConverter,
            editorValidator,
            Substitute.For<ICoordinateCaptureService>(),
            fileManager,
            dialogService,
            Substitute.For<IKeyCodeMapper>(),
            Substitute.For<IMacroPlayer>(),
            localizationService,
            new EditorActionDisplayFormatter(localizationService),
            uiDispatcher: ImmediateUiDispatcher.Instance);
        var editor = editorFactory.Create();

        var viewModel = new MainWindowViewModel(
            recording,
            playback,
            files,
            textExpansion,
            schedule,
            shortcut,
            trigger,
            settings,
            new EditorWorkspaceViewModel(editorFactory, dialogService, localizationService, ImmediateUiDispatcher.Instance, editor),
            hotkeyService,
            environmentInfo,
            externalUrlOpener,
            localizationService,
            extensionNotifier,
            updateService,
            platformStartupNotificationProviders,
            uiDispatcher: ImmediateUiDispatcher.Instance);

        return new MainWindowHarness
        {
            Recorder = recorder,
            Player = player,
            FileManager = fileManager,
            SettingsService = settingsService,
            HotkeyService = hotkeyService,
            FilesDialogService = filesDialogService,
            EditorDialogService = dialogService,
            ExternalUrlOpener = externalUrlOpener,
            SchedulerService = schedulerService,
            ShortcutService = shortcutService,
            LocalizationService = localizationService,
            LoadedMacroSession = loadedMacroSession,
            EditorConverter = editorConverter,
            EditorValidator = editorValidator,
            UiDispatcher = uiDispatcher,
            Recording = recording,
            Playback = playback,
            Files = files,
            TextExpansion = textExpansion,
            Schedule = schedule,
            Shortcut = shortcut,
            Trigger = trigger,
            Settings = settings,
            Editor = editor,
            ViewModel = viewModel,
        };
    }

    public void Dispose()
    {
        ViewModel.Dispose();
        ViewModel.Editor.Dispose();
        Recording.Dispose();
        Playback.Dispose();
        Files.Dispose();
        TextExpansion.Dispose();
        Schedule.Dispose();
        Shortcut.Dispose();
        Trigger.Dispose();
        Settings.Dispose();
    }
}
