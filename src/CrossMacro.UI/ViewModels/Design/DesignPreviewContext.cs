
namespace CrossMacro.UI.ViewModels;

internal sealed class DesignPreviewContext
{
    public DesignPreviewContext()
    {
        SettingsService = new DesignSettingsService(CreateSettings());
        HotkeySettings = CreateHotkeySettings();
        HotkeyService = new DesignGlobalHotkeyService();
        MousePositionProvider = new DesignMousePositionProvider();
        EnvironmentInfoProvider = new DesignEnvironmentInfoProvider();
        RuntimeContext = new DesignRuntimeContext();
        ExternalUrlOpener = new DesignExternalUrlOpener();
        RuntimeLogLevelService = new DesignRuntimeLogLevelService();
        ThemeService = new DesignThemeService(SettingsService.Current.Theme);
        LocalizationService = new LocalizationService();
        DialogService = new DesignDialogService();
        LoadedMacroSession = new LoadedMacroSession(LocalizationService);
        TextExpansionStore = new DesignTextExpansionStore();
        TextExpansionService = new DesignTextExpansionService();
        SchedulerService = new DesignSchedulerService();
        ShortcutService = new DesignShortcutService();
        MacroRecorder = new DesignMacroRecorder();
        MacroPlayer = new DesignMacroPlayer();
        MacroFileManager = new DesignMacroFileManager();
        EditorActionConverter = new DesignEditorActionConverter();
        EditorActionValidator = new DesignEditorActionValidator();
        CoordinateCaptureService = new DesignCoordinateCaptureService();
        KeyCodeMapper = new DesignKeyCodeMapper();
        TimeProvider = new DesignTimeProvider();
        TriggerService = new DesignTriggerService();
        ProfileManager = new DesignProfileManager();
    }

    public DesignSettingsService SettingsService { get; }

    public HotkeySettings HotkeySettings { get; }

    public DesignGlobalHotkeyService HotkeyService { get; }

    public DesignMousePositionProvider MousePositionProvider { get; }

    public DesignEnvironmentInfoProvider EnvironmentInfoProvider { get; }

    public DesignRuntimeContext RuntimeContext { get; }

    public DesignExternalUrlOpener ExternalUrlOpener { get; }

    public DesignRuntimeLogLevelService RuntimeLogLevelService { get; }

    public DesignThemeService ThemeService { get; }

    public DesignDialogService DialogService { get; }

    public LoadedMacroSession LoadedMacroSession { get; }

    public DesignTextExpansionStore TextExpansionStore { get; }

    public DesignTextExpansionService TextExpansionService { get; }

    public DesignSchedulerService SchedulerService { get; }

    public DesignShortcutService ShortcutService { get; }

    public DesignMacroRecorder MacroRecorder { get; }

    public DesignMacroPlayer MacroPlayer { get; }

    public DesignMacroFileManager MacroFileManager { get; }

    public DesignEditorActionConverter EditorActionConverter { get; }

    public DesignEditorActionValidator EditorActionValidator { get; }

    public DesignCoordinateCaptureService CoordinateCaptureService { get; }

    public DesignKeyCodeMapper KeyCodeMapper { get; }

    public DesignTimeProvider TimeProvider { get; }

    public DesignTriggerService TriggerService { get; }

    public DesignProfileManager ProfileManager { get; }

    public LocalizationService LocalizationService { get; }

    private static AppSettings CreateSettings()
    {
        return new AppSettings
        {
            EnableTrayIcon = true,
            StartMinimized = true,
            PlaybackSpeed = 1.25,
            IsLooping = true,
            LoopCount = 3,
            LoopDelayMs = 250,
            CountdownSeconds = 3,
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true,
            ForceRelativeCoordinates = true,
            SkipInitialZeroZero = true,
            EnableTextExpansion = true,
            CheckForUpdates = true,
            LogLevel = "Information",
            Theme = "Nord",
            Language = "en",
        };
    }

    private static HotkeySettings CreateHotkeySettings()
    {
        return new HotkeySettings
        {
            RecordingHotkey = "Ctrl+Alt+R",
            PlaybackHotkey = "Ctrl+Alt+P",
            PauseHotkey = "Ctrl+Alt+Space",
        };
    }
}
