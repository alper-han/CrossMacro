using CrossMacro.Application.Profiles;
using CrossMacro.Core.Services.Runtime;
using CrossMacro.Core.Services.TextExpansion;
using CrossMacro.UI.Services.Settings;

namespace CrossMacro.UI.ViewModels.Settings;

/// <summary>
/// ViewModel for the Settings tab - handles hotkey and application settings
/// </summary>
public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    public string AppVersion { get; } = CrossMacro.UI.Program.GetDisplayVersionString();

    // External snapshots update presentation without re-running user-change effects.
    private bool _isRefreshingPresentation;
    private static readonly Uri RepositoryUri = new("https://github.com/alper-han/CrossMacro", UriKind.Absolute);

    private readonly ISystemThemePreferenceService? _systemThemePreferenceService;
    private ThemePreferenceMode _selectedThemeMode;
    private ThemePreferenceOption[] _themeSelectionOptions;
    private ThemePreferenceOption? _selectedThemeSelection;
    private readonly ISettingsService _settingsService;
    private readonly ITextExpansionService _textExpansionService;
    private readonly HotkeySettings _hotkeySettings;
    private readonly IExternalUrlOpener _externalUrlOpener;
    private readonly IRuntimeLogLevelService _runtimeLogLevelService;
    private readonly IThemeService _themeService;
    private readonly IThemeDirectoryResolver? _themeDirectoryResolver;
    private readonly DirectoryOpener? _directoryOpener;
    private readonly SettingsChangeCoordinator _settingsChanges;
    private AppSettings _settingsDraft;
    private AppSettings _lastSubmittedSettings;
    private Task? _settingsPersistenceTask;

    private bool _enableTrayIcon;
    private bool _startMinimized;
    private bool _hideToTrayOnPlayback;
    private bool _hideToTrayOnRecording;
    private bool _disposed;

    internal Task? SettingsPersistenceTask => Volatile.Read(ref _settingsPersistenceTask);

    [ObservableProperty]
    private string _recordingHotkey;

    [ObservableProperty]
    private string _playbackHotkey;

    [ObservableProperty]
    private string _pauseHotkey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedLogLevelOption))]
    private string _selectedLogLevel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedLanguageOption))]
    private string _selectedLanguage;

    /// <summary>
    /// Event fired when tray icon setting changes
    /// </summary>
    public event EventHandler<bool>? TrayIconEnabledChanged;

    public SettingsViewModel(
        IGlobalHotkeyService hotkeyService,
        ISettingsService settingsService,
        ITextExpansionService textExpansionService,
        HotkeySettings hotkeySettings,
        IExternalUrlOpener externalUrlOpener,
        IRuntimeLogLevelService runtimeLogLevelService,
        IThemeService themeService,
        IRuntimeContext runtimeContext,
        ILocalizationService localizationService,
        SettingsChangeCoordinator settingsChanges,
        IProfileManager? profileManager = null,
        IDialogService? dialogService = null,
        IManageProfile? manageProfile = null,
        IThemeDirectoryResolver? themeDirectoryResolver = null,
        DirectoryOpener? directoryOpener = null,
        IUiDispatcher? uiDispatcher = null,
        ISystemThemePreferenceService? systemThemePreferenceService = null)
        : base(uiDispatcher)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(runtimeLogLevelService);
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(runtimeContext);
        ArgumentNullException.ThrowIfNull(localizationService);
        ArgumentNullException.ThrowIfNull(settingsChanges);

        GlobalHotkeyService = hotkeyService;
        _settingsService = settingsService;
        _settingsChanges = settingsChanges;
        _settingsDraft = settingsService.AccessCurrent(AppSettingsSnapshot.Copy);
        _lastSubmittedSettings = AppSettingsSnapshot.Copy(_settingsDraft);
        _textExpansionService = textExpansionService;
        _hotkeySettings = hotkeySettings;
        _externalUrlOpener = externalUrlOpener;
        _runtimeLogLevelService = runtimeLogLevelService;
        _themeService = themeService;
        _themeDirectoryResolver = themeDirectoryResolver;
        _directoryOpener = directoryOpener;
        _systemThemePreferenceService = systemThemePreferenceService;
        _selectedThemeMode = _settingsDraft.ThemeMode;
        LocalizationService = localizationService;
        _themeSelectionOptions = SettingsThemeOptions.Create(_themeService, LocalizationService);
        LogLevels = CreateLogLevelOptions();
        LocalizationService.CultureChanged += OnCultureChanged;
        SubscribeThemePreferenceEvents(_systemThemePreferenceService);

        _recordingHotkey = _hotkeySettings.RecordingHotkey;
        _playbackHotkey = _hotkeySettings.PlaybackHotkey;
        _pauseHotkey = _hotkeySettings.PauseHotkey;
        _enableTrayIcon = _settingsDraft.EnableTrayIcon;
        _startMinimized = _settingsDraft.StartMinimized;
        _hideToTrayOnPlayback = _settingsDraft.HideToTrayOnPlayback;
        _hideToTrayOnRecording = _settingsDraft.HideToTrayOnRecording;
        _selectedLogLevel = _settingsDraft.LogLevel;
        _selectedTheme = _settingsDraft.Theme;
        _selectedThemeSelection = SettingsThemeOptions.Find(_themeSelectionOptions, _settingsDraft.ThemeMode, _selectedTheme) ?? _themeSelectionOptions[0];
        _selectedLanguage = SettingsLanguageCatalog.Normalize(_settingsDraft.Language);
        _settingsDraft.Language = _selectedLanguage;
        _ = settingsService.AccessCurrent(settings => settings.Language = _selectedLanguage);
        _lastSubmittedSettings.Language = _selectedLanguage;
        AvailableLanguages = SettingsLanguageCatalog.CreateOptions(LocalizationService);
        RefreshLanguageOptions();
        IsUpdateSettingsVisible = !runtimeContext.IsFlatpak;
        Profiles = new SettingsProfilesViewModel(profileManager, dialogService, manageProfile, localizationService, settingsChanges, uiDispatcher);
        Profiles.ProfileSettingsInvalidated += OnProfileSettingsInvalidated;
    }

    public bool IsUpdateSettingsVisible { get; }

    public IGlobalHotkeyService GlobalHotkeyService { get; }

    public ILocalizationService LocalizationService { get; }

    public SettingsProfilesViewModel Profiles { get; }

    partial void OnRecordingHotkeyChanged(string value)
    {
        if (_isRefreshingPresentation) { return; }
        _hotkeySettings.RecordingHotkey = value;
        UpdateHotkeys();
    }

    partial void OnPlaybackHotkeyChanged(string value)
    {
        if (_isRefreshingPresentation) { return; }
        _hotkeySettings.PlaybackHotkey = value;
        UpdateHotkeys();
    }

    partial void OnPauseHotkeyChanged(string value)
    {
        if (_isRefreshingPresentation) { return; }
        _hotkeySettings.PauseHotkey = value;
        UpdateHotkeys();
    }

    public bool EnableTrayIcon
    {
        get => _enableTrayIcon;
        set => ApplyTrayPreference(TrayPreference.EnableTrayIcon, value);
    }

    public bool HideToTrayOnPlayback
    {
        get => _hideToTrayOnPlayback;
        set => ApplyTrayPreference(TrayPreference.HideToTrayOnPlayback, value);
    }

    public bool HideToTrayOnRecording
    {
        get => _hideToTrayOnRecording;
        set => ApplyTrayPreference(TrayPreference.HideToTrayOnRecording, value);
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        set => ApplyTrayPreference(TrayPreference.StartMinimized, value);
    }

    private void ApplyTrayPreference(TrayPreference preference, bool value)
    {
        var before = new TrayPreferences(_enableTrayIcon, _startMinimized, _hideToTrayOnPlayback, _hideToTrayOnRecording);
        var after = TrayPreferencePolicy.Apply(before, preference, value);
        if (before == after) { return; }

        _settingsDraft.EnableTrayIcon = _enableTrayIcon = after.EnableTrayIcon;
        _settingsDraft.StartMinimized = _startMinimized = after.StartMinimized;
        _settingsDraft.HideToTrayOnPlayback = _hideToTrayOnPlayback = after.HideToTrayOnPlayback;
        _settingsDraft.HideToTrayOnRecording = _hideToTrayOnRecording = after.HideToTrayOnRecording;

        // Publish the requested field first, then any dependent fields in their established order.
        var requestedProperty = preference switch
        {
            TrayPreference.EnableTrayIcon => nameof(EnableTrayIcon),
            TrayPreference.StartMinimized => nameof(StartMinimized),
            TrayPreference.HideToTrayOnPlayback => nameof(HideToTrayOnPlayback),
            TrayPreference.HideToTrayOnRecording => nameof(HideToTrayOnRecording),
            _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, message: null),
        };
        var propertyNames = new List<string> { requestedProperty };
        AddDependentChange(nameof(EnableTrayIcon), before.EnableTrayIcon != after.EnableTrayIcon);
        AddDependentChange(nameof(StartMinimized), before.StartMinimized != after.StartMinimized);
        AddDependentChange(nameof(HideToTrayOnPlayback), before.HideToTrayOnPlayback != after.HideToTrayOnPlayback);
        AddDependentChange(nameof(HideToTrayOnRecording), before.HideToTrayOnRecording != after.HideToTrayOnRecording);
        foreach (var propertyName in propertyNames) { OnPropertyChanged(propertyName); }

        QueueSettingsPersistence(
            before.EnableTrayIcon != after.EnableTrayIcon
                ? () => { TrayIconEnabledChanged?.Invoke(this, _enableTrayIcon); return Task.CompletedTask; }
        : null,
            propertyNames.ToArray());

        void AddDependentChange(string propertyName, bool changed)
        {
            if (changed && !string.Equals(propertyName, requestedProperty, StringComparison.Ordinal))
            { propertyNames.Add(propertyName); }
        }
    }

    // Kept manual: no backing field, state proxies ISettingsService directly.
    public bool EnableTextExpansion
    {
        get => _settingsDraft.EnableTextExpansion;
        set
        {
            SynchronizeDraftIfIdle();
            if (_settingsDraft.EnableTextExpansion != value)
            {

                _settingsDraft.EnableTextExpansion = value;

                QueueSettingsPersistence(
                    async () =>
                    {
                        if (_settingsDraft.EnableTextExpansion)
                        {
                            _textExpansionService.Start();
                        }
                        else
                        {
                            await _textExpansionService.StopExpansionAsync(CancellationToken.None).ConfigureAwait(false);
                        }
                    },
                    nameof(EnableTextExpansion));
                OnPropertyChanged();
            }
        }
    }

    // Kept manual: no backing field, state proxies ISettingsService directly.
    public bool CheckForUpdates
    {
        get => _settingsDraft.CheckForUpdates;
        set
        {
            SynchronizeDraftIfIdle();
            if (_settingsDraft.CheckForUpdates != value)
            {

                _settingsDraft.CheckForUpdates = value;
                OnPropertyChanged();

                QueueSettingsPersistence(
                    nameof(CheckForUpdates));
            }
        }
    }

    partial void OnSelectedLogLevelChanged(string? oldValue, string newValue)
    {
        if (_isRefreshingPresentation) { return; }

        _settingsDraft.LogLevel = newValue;
        _runtimeLogLevelService.SetLogLevel(newValue);

        QueueSettingsPersistence(
            nameof(SelectedLogLevel));
    }

    /// <summary>
    /// Available log levels for the ComboBox
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedLogLevelOption))]
    public partial IReadOnlyList<LogLevelOption> LogLevels { get; private set; }

    public LogLevelOption? SelectedLogLevelOption
    {
        get => LogLevels.FirstOrDefault(option => string.Equals(option.Value, SelectedLogLevel, StringComparison.Ordinal));
        set
        {
            if (value is not null)
            {
                SelectedLogLevel = value.Value;
            }
        }
    }

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    public LanguageOption? SelectedLanguageOption
    {
        get => AvailableLanguages.FirstOrDefault(option => string.Equals(option.Code, SelectedLanguage, StringComparison.Ordinal));
        set
        {
            if (value is null)
            {
                return;
            }

            SelectedLanguage = value.Code;
        }
    }

    partial void OnSelectedLanguageChanged(string? oldValue, string newValue)
    {
        if (_isRefreshingPresentation) { return; }

        _settingsDraft.Language = newValue;
        LocalizationService.SetCulture(newValue);
        RefreshLanguageOptions();

        QueueSettingsPersistence(
            nameof(SelectedLanguage),
            nameof(AvailableLanguages),
            nameof(SelectedLanguageOption));
    }

    private void RefreshLanguageOptions()
    {
        foreach (var option in AvailableLanguages)
        {
            option.DisplayName = SettingsLanguageCatalog.GetDisplayName(LocalizationService, option.Code);
        }

        OnPropertyChanged(nameof(AvailableLanguages));
        OnPropertyChanged(nameof(SelectedLanguageOption));
    }

    private string _selectedTheme;
    private bool _isRefreshingThemes;

    // Ignore transient refresh selections and reject failed theme applications.
    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_isRefreshingThemes || string.IsNullOrWhiteSpace(value)) { return; }
            var previousTheme = _selectedTheme;
            _selectedTheme = value;
            var option = _themeSelectionOptions.FirstOrDefault(candidate =>
                string.Equals(candidate.ThemeName, value, StringComparison.OrdinalIgnoreCase));
            if (option is null && string.Equals(previousTheme, value, StringComparison.OrdinalIgnoreCase))
            {
                option = _themeSelectionOptions.FirstOrDefault(candidate => !candidate.IsSystem && string.Equals(candidate.ThemeName, value, StringComparison.OrdinalIgnoreCase));
            }
            if (option is not null) { SelectedThemeSelection = option; }
            if (!ReferenceEquals(_selectedThemeSelection, option)) { _selectedTheme = previousTheme; }
        }
    }

    public IEnumerable<string> AvailableThemes => _themeService.AvailableThemes;
    public IReadOnlyList<ThemePreferenceOption> ThemeSelectionOptions => _themeSelectionOptions;

    public ThemePreferenceOption? SelectedThemeSelection
    {
        get => _selectedThemeSelection;
        set
        {
            if (_isRefreshingPresentation || value is null || ReferenceEquals(_selectedThemeSelection, value)) { return; }
            var isSystem = value.IsSystem;
            var theme = isSystem ? _themeService.CurrentTheme : value.ThemeName;
            if (string.IsNullOrWhiteSpace(theme)) { return; }
            if (!_themeService.TryApplyTheme(theme, out var error))
            {
                Log.Warning("Theme apply failed for '{Theme}': {Error}", theme, error);
                return;
            }

            _selectedThemeSelection = value;
            _selectedTheme = theme;
            _selectedThemeMode = isSystem ? ThemePreferenceMode.System : ThemePreferenceMode.Manual;
            _settingsDraft.Theme = theme;
            _settingsDraft.ThemeMode = _selectedThemeMode;
            _ = _themeService.TrySetPreferenceMode(_selectedThemeMode, out _);
            _systemThemePreferenceService?.SetMode(_selectedThemeMode);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedTheme));
            QueueSettingsPersistence(nameof(SelectedThemeSelection), nameof(SelectedTheme));
        }
    }

    public ThemePreferenceMode SelectedThemeMode
    {
        get => _selectedThemeMode;
        set
        {
            if (_isRefreshingPresentation || _selectedThemeMode == value) { return; }
            _selectedThemeMode = value;
            if (value is ThemePreferenceMode.System)
            {
                SelectedThemeSelection = _themeSelectionOptions[0];
            }
            else
            {
                var selected = _themeSelectionOptions.FirstOrDefault(option =>
                    string.Equals(option.ThemeName, _selectedTheme, StringComparison.OrdinalIgnoreCase));
                if (selected is not null) { SelectedThemeSelection = selected; }
            }
        }
    }

    private void OnSystemThemeResolved(object? sender, string themeName)
    {
        if (_selectedThemeMode is not ThemePreferenceMode.System || string.Equals(_selectedTheme, themeName, StringComparison.Ordinal)) { return; }
        _selectedTheme = themeName;
        _settingsDraft.Theme = themeName;
        OnPropertyChanged(nameof(SelectedTheme));
    }

    private void OnCultureChanged(object? sender, EventArgs args)
    {
        LogLevels = CreateLogLevelOptions();
        _themeSelectionOptions = SettingsThemeOptions.Create(_themeService, LocalizationService);
        _selectedThemeSelection = SettingsThemeOptions.Find(_themeSelectionOptions, _selectedThemeMode, _selectedTheme);
        OnPropertyChanged(nameof(ThemeSelectionOptions));
        OnPropertyChanged(nameof(SelectedThemeSelection));
    }

    private LogLevelOption[] CreateLogLevelOptions() =>
    [
        new("Debug", LocalizationService["Settings_LogLevelDebug"]),
        new("Information", LocalizationService["Settings_LogLevelInformation"]),
        new("Warning", LocalizationService["Settings_LogLevelWarning"]),
        new("Error", LocalizationService["Settings_LogLevelError"]),
    ];

    [RelayCommand]
    private void RefreshThemes()
    {

        string refreshedTheme;

        _isRefreshingThemes = true;
        try
        {
            if (!_themeService.TryRefreshThemes(out var refreshError))
            {
                Log.Warning("Theme refresh completed with warnings: {Error}", refreshError);
            }

            refreshedTheme = _themeService.CurrentTheme;
            _themeSelectionOptions = SettingsThemeOptions.Create(_themeService, LocalizationService);
            _selectedThemeSelection = SettingsThemeOptions.Find(_themeSelectionOptions, _selectedThemeMode, refreshedTheme) ?? _themeSelectionOptions[0];
            OnPropertyChanged(nameof(AvailableThemes));
            OnPropertyChanged(nameof(ThemeSelectionOptions));
            OnPropertyChanged(nameof(SelectedThemeSelection));
            if (string.Equals(_selectedTheme, refreshedTheme, StringComparison.Ordinal))
            {
                OnPropertyChanged(nameof(SelectedTheme));
                return;
            }

            _selectedTheme = refreshedTheme;
            _settingsDraft.Theme = refreshedTheme;
            OnPropertyChanged(nameof(SelectedTheme));
        }
        finally
        {
            _isRefreshingThemes = false;
        }

        QueueSettingsPersistence(
            nameof(SelectedTheme));
    }

    [RelayCommand]
    private async Task OpenThemesFolderAsync()
    {
        // Design-time and minimal compositions may omit these optional dependencies.
        if (_themeDirectoryResolver is null || _directoryOpener is null)
        {
            return;
        }

        try
        {
            await _directoryOpener.OpenAsync(_themeDirectoryResolver.GetThemeDirectoryPath()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning("Failed to open themes folder: {Error}", ex.Message);
        }
    }

    private void OnProfileSettingsInvalidated(object? sender, EventArgs e)
    {
        _settingsDraft = _settingsService.AccessCurrent(AppSettingsSnapshot.Copy);
        _lastSubmittedSettings = AppSettingsSnapshot.Copy(_settingsDraft);
        // Update hotkey presentation without reapplying registrations.
        var wasRefreshingPresentation = _isRefreshingPresentation;
        _isRefreshingPresentation = true;
        try
        {
            RecordingHotkey = _hotkeySettings.RecordingHotkey;
            PlaybackHotkey = _hotkeySettings.PlaybackHotkey;
            PauseHotkey = _hotkeySettings.PauseHotkey;
        }
        finally
        {
            _isRefreshingPresentation = wasRefreshingPresentation;
        }

        OnPropertyChanged(nameof(RecordingHotkey));
        OnPropertyChanged(nameof(PlaybackHotkey));
        OnPropertyChanged(nameof(PauseHotkey));
        OnPropertyChanged(nameof(EnableTextExpansion));
        OnPropertyChanged(nameof(CheckForUpdates));
    }

    /// <summary>
    /// Validates that a new hotkey for one slot does not collide with the other two slots.
    /// Returns a localized error message when invalid.
    /// </summary>
    public (bool IsValid, string ErrorMessage) ValidateRecordingHotkey(string newHotkey) =>
        ValidateHotkeyAssignment(newHotkey, (PlaybackHotkey, "Settings_TogglePlayback"), (PauseHotkey, "Settings_PauseResumePlayback"));

    public (bool IsValid, string ErrorMessage) ValidatePlaybackHotkey(string newHotkey) =>
        ValidateHotkeyAssignment(newHotkey, (RecordingHotkey, "Settings_ToggleRecording"), (PauseHotkey, "Settings_PauseResumePlayback"));

    public (bool IsValid, string ErrorMessage) ValidatePauseHotkey(string newHotkey) =>
        ValidateHotkeyAssignment(newHotkey, (RecordingHotkey, "Settings_ToggleRecording"), (PlaybackHotkey, "Settings_TogglePlayback"));

    private (bool IsValid, string ErrorMessage) ValidateHotkeyAssignment(
        string newHotkey,
        (string Hotkey, string LabelKey) first,
        (string Hotkey, string LabelKey) second)
    {
        foreach (var (hotkey, labelKey) in (ReadOnlySpan<(string, string)>)[first, second])
        {
            if (string.Equals(newHotkey, hotkey, StringComparison.Ordinal))
            {
                var message = string.Format(
                    LocalizationService.CurrentCulture,
                    LocalizationService["Settings_HotkeyAlreadyAssignedTo"],
                    LocalizationService[labelKey]);
                return (false, message);
            }
        }

        return (true, string.Empty);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Profiles.ProfileSettingsInvalidated -= OnProfileSettingsInvalidated;
        Profiles.Dispose();
        LocalizationService.CultureChanged -= OnCultureChanged;
        UnsubscribeThemePreferenceEvents(_systemThemePreferenceService);
    }

    private void SubscribeThemePreferenceEvents(ISystemThemePreferenceService? service)
    {
        if (service is { } activeService) { activeService.ThemeResolved += OnSystemThemeResolved; }
    }

    private void UnsubscribeThemePreferenceEvents(ISystemThemePreferenceService? service)
    {
        if (service is { } activeService) { activeService.ThemeResolved -= OnSystemThemeResolved; }
    }
    private void UpdateHotkeys()
    {
        try
        {
            if (GlobalHotkeyService.IsRunning)
            {
                GlobalHotkeyService.UpdateHotkeys(
                    _hotkeySettings.RecordingHotkey,
                    _hotkeySettings.PlaybackHotkey,
                    _hotkeySettings.PauseHotkey);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Hotkey update error");
        }
    }

    /// <summary>
    /// Open the GitHub repository
    /// </summary>
    public void OpenGitHub()
    {
        _ = OpenGitHubAsync();
    }

    private async Task OpenGitHubAsync()
    {
        try
        {
            await _externalUrlOpener.OpenAsync(RepositoryUri).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to open GitHub URL");
        }
    }

    private void SynchronizeDraftIfIdle()
    {
        if (Volatile.Read(ref _settingsPersistenceTask)?.IsCompleted is not false)
        {
            _settingsDraft = _settingsService.AccessCurrent(AppSettingsSnapshot.Copy);
            _lastSubmittedSettings = AppSettingsSnapshot.Copy(_settingsDraft);
        }
    }

    private void QueueSettingsPersistence(params string[] propertyNames) => QueueSettingsPersistence(applyRuntimeEffect: null, propertyNames);

    private void QueueSettingsPersistence(Func<Task>? applyRuntimeEffect, params string[] propertyNames)
    {
        Func<Func<bool>, Task>? runtimeEffect = applyRuntimeEffect is null
            ? null
            : isCurrent => UiDispatcher.InvokeAsync(async () =>
            {
                if (isCurrent()) { await applyRuntimeEffect().ConfigureAwait(false); }
            });
        var task = SettingsDraftPersistence.SubmitAsync(
            _settingsChanges, _settingsDraft, ref _lastSubmittedSettings,
            () => RestoreAfterPersistenceFailureAsync(propertyNames),
            "Failed to persist settings change",
            runtimeEffect);
        Volatile.Write(ref _settingsPersistenceTask, task);
    }

    private Task RestoreAfterPersistenceFailureAsync(string[] propertyNames) =>
        UiDispatcher.InvokeAsync(async () =>
        {
            var trayEnabled = _enableTrayIcon;
            var textExpansionEnabled = _settingsDraft.EnableTextExpansion;
            RestorePresentationFromSettings();
            foreach (var propertyName in propertyNames) { OnPropertyChanged(propertyName); }
            if (trayEnabled != _enableTrayIcon) { TrayIconEnabledChanged?.Invoke(this, _enableTrayIcon); }
            if (textExpansionEnabled != _settingsDraft.EnableTextExpansion)
            {
                if (_settingsDraft.EnableTextExpansion) { _textExpansionService.Start(); }
                else { await _textExpansionService.StopExpansionAsync(CancellationToken.None).ConfigureAwait(false); }
            }
        });

    private void RestorePresentationFromSettings()
    {
        var current = _settingsService.AccessCurrent(AppSettingsSnapshot.Copy);
        if (!string.Equals(_selectedTheme, current.Theme, StringComparison.Ordinal)
            && !_themeService.TryApplyTheme(current.Theme, out var error))
        {
            Log.Warning("Theme rollback failed: {Error}", error);
            var failedTheme = current.Theme;
            current = _settingsService.AccessCurrent(settings =>
            {
                if (string.Equals(settings.Theme, failedTheme, StringComparison.Ordinal))
                {
                    settings.Theme = _themeService.CurrentTheme;
                }
                return AppSettingsSnapshot.Copy(settings);
            });
        }
        if (!string.Equals(SelectedLogLevel, current.LogLevel, StringComparison.Ordinal)) { _runtimeLogLevelService.SetLogLevel(current.LogLevel); }
        if (!string.Equals(SelectedLanguage, current.Language, StringComparison.Ordinal)) { LocalizationService.SetCulture(current.Language); }
        var wasRefreshingPresentation = _isRefreshingPresentation;
        _isRefreshingPresentation = true;
        try
        {
            SelectedLogLevel = current.LogLevel;
            SelectedLanguage = current.Language;
        }
        finally
        {
            _isRefreshingPresentation = wasRefreshingPresentation;
        }
        _selectedTheme = current.Theme;
        _selectedThemeMode = current.ThemeMode;
        _selectedThemeSelection = SettingsThemeOptions.Find(_themeSelectionOptions, current.ThemeMode, current.Theme) ?? _themeSelectionOptions[0];
        _ = _themeService.TrySetPreferenceMode(current.ThemeMode, out _);
        _systemThemePreferenceService?.SetMode(current.ThemeMode);
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedThemeMode));
        OnPropertyChanged(nameof(SelectedThemeSelection));
        _enableTrayIcon = current.EnableTrayIcon;
        _startMinimized = current.StartMinimized;
        _hideToTrayOnPlayback = current.HideToTrayOnPlayback;
        _hideToTrayOnRecording = current.HideToTrayOnRecording;
        _settingsDraft = AppSettingsSnapshot.Copy(current);
        _lastSubmittedSettings = AppSettingsSnapshot.Copy(current);
        RefreshLanguageOptions();
    }
}
