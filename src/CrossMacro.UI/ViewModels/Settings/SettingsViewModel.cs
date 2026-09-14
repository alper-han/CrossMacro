
namespace CrossMacro.UI.ViewModels.Settings;

/// <summary>
/// ViewModel for the Settings tab - handles hotkey and application settings
/// </summary>
public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private static readonly Uri RepositoryUri = new("https://github.com/alper-han/CrossMacro", UriKind.Absolute);

    internal static readonly IReadOnlyList<SupportedLanguageDescriptor> SupportedLanguages =
    [
        new("en", "Language_English", "English", isDefault: true),
        new("ar", "Language_Arabic", "Arabic"),
        new("zh", "Language_Chinese", "Chinese"),
        new("fr", "Language_French", "French"),
        new("ja", "Language_Japanese", "Japanese"),
        new("pt", "Language_Portuguese", "Portuguese"),
        new("ru", "Language_Russian", "Russian"),
        new("es", "Language_Spanish", "Spanish"),
        new("tr", "Language_Turkish", "Turkish"),
    ];

    internal static IReadOnlyList<string> SupportedLanguageCodes { get; } = SupportedLanguages
        .Select(language => language.Code)
        .ToArray();

    private readonly ISettingsService _settingsService;
    private readonly ITextExpansionService _textExpansionService;
    private readonly HotkeySettings _hotkeySettings;
    private readonly IExternalUrlOpener _externalUrlOpener;
    private readonly IRuntimeLogLevelService _runtimeLogLevelService;
    private readonly IThemeService _themeService;
    private readonly IThemeDirectoryResolver? _themeDirectoryResolver;
    private readonly IDirectoryOpener? _directoryOpener;
    private readonly IProfileManager? _profileManager;
    private readonly IDialogService? _dialogService;
    private readonly IManageProfile? _manageProfile;
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
    private string _selectedLogLevel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedLanguageOption))]
    private string _selectedLanguage;

    [ObservableProperty]
    private string _newProfileName = string.Empty;

    [ObservableProperty]
    private ProfileInfo? _selectedProfile;

    /// <summary>
    /// Event fired when tray icon setting changes
    /// </summary>
    public event EventHandler<bool>? TrayIconEnabledChanged;

    public event EventHandler<string>? ProfileOperationFailed;

    public SettingsViewModel(
        IGlobalHotkeyService hotkeyService,
        ISettingsService settingsService,
        ITextExpansionService textExpansionService,
        HotkeySettings hotkeySettings,
        IExternalUrlOpener externalUrlOpener,
        IRuntimeLogLevelService runtimeLogLevelService,
        IThemeService themeService,
        ILocalizationService? localizationService = null,
        IRuntimeContext? runtimeContext = null,
        IProfileManager? profileManager = null,
        IDialogService? dialogService = null,
        IManageProfile? manageProfile = null,
        IUiDispatcher? uiDispatcher = null,
        SettingsChangeCoordinator? settingsChanges = null)
        : this(
            hotkeyService,
            settingsService,
            textExpansionService,
            hotkeySettings,
            externalUrlOpener,
            runtimeLogLevelService,
            themeService,
            runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext)),
            localizationService,
            profileManager,
            dialogService,
            manageProfile, uiDispatcher: uiDispatcher, settingsChanges: settingsChanges)
    { /* Empty */ }

    public SettingsViewModel(
        IGlobalHotkeyService hotkeyService,
        ISettingsService settingsService,
        ITextExpansionService textExpansionService,
        HotkeySettings hotkeySettings,
        IExternalUrlOpener externalUrlOpener,
        IRuntimeLogLevelService runtimeLogLevelService,
        IThemeService themeService,
        IRuntimeContext runtimeContext,
        ILocalizationService? localizationService = null,
        IProfileManager? profileManager = null,
        IDialogService? dialogService = null,
        IManageProfile? manageProfile = null,
        IThemeDirectoryResolver? themeDirectoryResolver = null,
        IDirectoryOpener? directoryOpener = null,
        IUiDispatcher? uiDispatcher = null,
        SettingsChangeCoordinator? settingsChanges = null)
        : base(uiDispatcher)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(runtimeLogLevelService);
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(runtimeContext);

        GlobalHotkeyService = hotkeyService;
        _settingsService = settingsService;
        _settingsChanges = settingsChanges ?? new SettingsChangeCoordinator(settingsService);
        _settingsDraft = AppSettingsSnapshot.Copy(settingsService.Current);
        _lastSubmittedSettings = AppSettingsSnapshot.Copy(_settingsDraft);
        _textExpansionService = textExpansionService;
        _hotkeySettings = hotkeySettings;
        _externalUrlOpener = externalUrlOpener;
        _runtimeLogLevelService = runtimeLogLevelService;
        _themeService = themeService;
        LocalizationService = localizationService ?? new LocalizationService();
        _profileManager = profileManager;
        _dialogService = dialogService;
        _manageProfile = manageProfile ?? (profileManager is null ? null : new ManageProfile(profileManager));
        _themeDirectoryResolver = themeDirectoryResolver;
        _directoryOpener = directoryOpener;

        AvailableProfiles = [];
        _recordingHotkey = _hotkeySettings.RecordingHotkey;
        _playbackHotkey = _hotkeySettings.PlaybackHotkey;
        _pauseHotkey = _hotkeySettings.PauseHotkey;
        _enableTrayIcon = _settingsDraft.EnableTrayIcon;
        _startMinimized = _settingsDraft.StartMinimized;
        _hideToTrayOnPlayback = _settingsDraft.HideToTrayOnPlayback;
        _hideToTrayOnRecording = _settingsDraft.HideToTrayOnRecording;
        _selectedLogLevel = _settingsDraft.LogLevel;
        _selectedTheme = _settingsDraft.Theme;
        _selectedLanguage = NormalizeSupportedLanguage(_settingsDraft.Language);
        _settingsDraft.Language = _selectedLanguage;
        settingsService.Current.Language = _selectedLanguage;
        _lastSubmittedSettings.Language = _selectedLanguage;
        AvailableLanguages = CreateLanguageOptions();
        RefreshLanguageOptions();
        IsUpdateSettingsVisible = !runtimeContext.IsFlatpak;
        IsTraySettingsVisible = TrayIconService.IsTraySupported(runtimeContext);
        RefreshProfileState();
        _profileManager?.ProfileChanged += OnProfileChanged;
    }

    public bool IsUpdateSettingsVisible { get; }

    public IGlobalHotkeyService GlobalHotkeyService { get; }

    public ILocalizationService LocalizationService { get; }

    [ObservableProperty]
    public partial IReadOnlyList<ProfileInfo> AvailableProfiles { get; private set; }

    [ObservableProperty]
    public partial bool IsProfileOperationInProgress { get; private set; }

    /// <summary>
    /// Tray icon settings are hidden in Flatpak where StatusNotifierItem is not supported
    /// </summary>
    public bool IsTraySettingsVisible { get; }

    partial void OnRecordingHotkeyChanged(string value)
    {
        _hotkeySettings.RecordingHotkey = value;
        UpdateHotkeys();
    }

    partial void OnPlaybackHotkeyChanged(string value)
    {
        _hotkeySettings.PlaybackHotkey = value;
        UpdateHotkeys();
    }

    partial void OnPauseHotkeyChanged(string value)
    {
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
        var after = TrayPreferencePolicy.Apply(before, preference, value, IsTraySettingsVisible);
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

        _ = TryPersistSettings(
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

                _ = TryPersistSettings(
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

                _ = TryPersistSettings(
                    nameof(CheckForUpdates));
            }
        }
    }

    partial void OnSelectedLogLevelChanged(string? oldValue, string newValue)
    {

        _settingsDraft.LogLevel = newValue;
        _runtimeLogLevelService.SetLogLevel(newValue);

        _ = TryPersistSettings(
            nameof(SelectedLogLevel));
    }

    /// <summary>
    /// Available log levels for the ComboBox
    /// </summary>
    public IEnumerable<string> LogLevels { get; } =
    [
        "Debug",
        "Information",
        "Warning",
        "Error",
    ];

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

        _settingsDraft.Language = newValue;
        LocalizationService.SetCulture(newValue);
        RefreshLanguageOptions();

        _ = TryPersistSettings(
            nameof(SelectedLanguage),
            nameof(AvailableLanguages),
            nameof(SelectedLanguageOption));
    }

    private void RefreshLanguageOptions()
    {
        foreach (var option in AvailableLanguages)
        {
            option.DisplayName = GetLanguageDisplayName(option.Code);
        }

        OnPropertyChanged(nameof(AvailableLanguages));
        OnPropertyChanged(nameof(SelectedLanguageOption));
    }

    private LanguageOption[] CreateLanguageOptions()
    {
        return SupportedLanguages
            .OrderByDescending(language => language.IsDefault)
            .ThenBy(language => language.EnglishName, StringComparer.Ordinal)
            .Select(language => new LanguageOption
            {
                Code = language.Code,
                DisplayName = GetLanguageDisplayName(language),
            })
            .ToArray();
    }

    private string GetLanguageDisplayName(string code)
    {
        var language = SupportedLanguages.FirstOrDefault(language =>
            string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase));
        return language is null
            ? LocalizationService[SupportedLanguages[0].ResourceKey]
            : GetLanguageDisplayName(language);
    }

    private string GetLanguageDisplayName(SupportedLanguageDescriptor language)
    {
        return LocalizationService[language.ResourceKey];
    }

    internal sealed record SupportedLanguageDescriptor
    {
        public SupportedLanguageDescriptor(
            string code,
            string resourceKey,
            string englishName,
            bool isDefault = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(englishName);

            Code = code;
            ResourceKey = resourceKey;
            EnglishName = englishName;
            IsDefault = isDefault;
        }

        public string Code { get; }
        public string ResourceKey { get; }
        public string EnglishName { get; }
        public bool IsDefault { get; }
    }

    private static string NormalizeSupportedLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "en";
        }

        var supportedLanguage = SupportedLanguages.FirstOrDefault(candidate =>
            string.Equals(candidate.Code, language, StringComparison.OrdinalIgnoreCase));
        return supportedLanguage?.Code ?? "en";
    }

    private string _selectedTheme;
    private bool _isRefreshingThemes;

    // Kept manual: ignores transient refresh selections and rejects failed theme applications.
    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_isRefreshingThemes || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!string.Equals(_selectedTheme, value, StringComparison.Ordinal))
            {
                if (!_themeService.TryApplyTheme(value, out var applyError))
                {
                    Log.Warning("Theme apply failed for '{Theme}': {Error}", value, applyError);
                    return;
                }

                _selectedTheme = value;
                _settingsDraft.Theme = value;
                OnPropertyChanged();

                _ = TryPersistSettings(
                    nameof(SelectedTheme));
            }
        }
    }

    public IEnumerable<string> AvailableThemes => _themeService.AvailableThemes;

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
            OnPropertyChanged(nameof(AvailableThemes));
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

        _ = TryPersistSettings(
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

    public async Task CreateProfileAsync()
    {
        var profileName = NewProfileName.Trim();
        if (_manageProfile is null || profileName.Length is 0)
        {
            return;
        }

        await RunProfileOperationAsync(async () =>
        {
            var createdProfile = (await _manageProfile.CreateAsync(new ProfileRequest(DisplayName: profileName), default).ConfigureAwait(false)).Profile ?? throw new InvalidOperationException("Profile was not returned after creation.");
            await RunOnUiThreadAsync(() =>
            {
                RefreshProfileState(createdProfile.Id);
                NewProfileName = string.Empty;
            }).ConfigureAwait(false);
        }, LocalizationService["Settings_ProfileCreateFailed"]).ConfigureAwait(false);
    }

    public async Task RenameSelectedProfileAsync()
    {
        var profileName = NewProfileName.Trim();
        var selectedProfile = SelectedProfile;
        if (_manageProfile is null || selectedProfile is null || profileName.Length is 0)
        {
            return;
        }

        await RunProfileOperationAsync(async () =>
        {
            _ = await _manageProfile.RenameAsync(new ProfileRequest(selectedProfile.Id, profileName), default).ConfigureAwait(false);

            await RunOnUiThreadAsync(() =>
            {
                RefreshProfileState(selectedProfile.Id);
                NewProfileName = string.Empty;
            }).ConfigureAwait(false);
        }, LocalizationService["Settings_ProfileRenameFailed"]).ConfigureAwait(false);
    }

    public async Task DeleteSelectedProfileAsync()
    {
        var selectedProfile = SelectedProfile;
        if (_manageProfile is null || selectedProfile is null)
        {
            return;
        }

        if (IsProfileOperationInProgress)
        {
            return;
        }

        if (_dialogService is not null)
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                LocalizationService["Settings_ProfileDeleteTitle"],
                string.Format(
                    LocalizationService.CurrentCulture,
                    LocalizationService["Settings_ProfileDeleteMessage"],
                    selectedProfile.Name)).ConfigureAwait(false);

            if (!confirmed)
            {
                return;
            }
        }

        await RunProfileOperationAsync(async () =>
        {
            _ = await _manageProfile.DeleteAsync(new ProfileRequest(Identifier: selectedProfile.Id), default).ConfigureAwait(false);

            await RunOnUiThreadAsync(() => RefreshProfileState()).ConfigureAwait(false);
        }, LocalizationService["Settings_ProfileDeleteFailed"]).ConfigureAwait(false);
    }

    public async Task SwitchProfileAsync()
    {
        var selectedProfile = SelectedProfile;
        if (_manageProfile is null || selectedProfile is null)
        {
            return;
        }

        _settingsChanges.InvalidatePendingChanges();
        await RunProfileOperationAsync(async () =>
        {
            _ = await _manageProfile.SwitchAsync(new ProfileRequest(Identifier: selectedProfile.Id), default).ConfigureAwait(false);

            await RunOnUiThreadAsync(() =>
            {
                RefreshProfileState(selectedProfile.Id);
                RefreshProfileSpecificSettings();
            }).ConfigureAwait(false);
        }, LocalizationService["Settings_ProfileSwitchFailed"]).ConfigureAwait(false);
    }



    public void RefreshProfileState(string? selectedProfileId = null)
    {
        PostToUiThread(() => RefreshProfileStateCore(selectedProfileId));
    }

    private void RefreshProfileStateCore(string? selectedProfileId)
    {
        if (_profileManager is null)
        {
            AvailableProfiles = [];
            SelectedProfile = null;
            return;
        }

        AvailableProfiles = _profileManager.Profiles.ToArray();
        var effectiveSelectedProfileId = selectedProfileId ?? _profileManager.ActiveProfile.Id;
        SelectedProfile = AvailableProfiles.FirstOrDefault(profile =>
                              string.Equals(profile.Id, effectiveSelectedProfileId, StringComparison.Ordinal))
                          ?? _profileManager.ActiveProfile;
    }

    public void RefreshProfileSpecificSettings()
    {
        PostToUiThread(RefreshProfileSpecificSettingsCore);
    }

    private void RefreshProfileSpecificSettingsCore()
    {
        _settingsDraft = AppSettingsSnapshot.Copy(_settingsService.Current);
        _lastSubmittedSettings = AppSettingsSnapshot.Copy(_settingsDraft);
        // Direct field writes: refreshing from settings must not re-apply hotkeys via setter hooks.
#pragma warning disable MVVMTK0034
        _recordingHotkey = _hotkeySettings.RecordingHotkey;
        _playbackHotkey = _hotkeySettings.PlaybackHotkey;
        _pauseHotkey = _hotkeySettings.PauseHotkey;
#pragma warning restore MVVMTK0034

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

    private async Task RunProfileOperationAsync(Func<Task> operation, string failureMessage)
    {
        var started = false;
        await RunOnUiThreadAsync(() =>
        {
            if (!IsProfileOperationInProgress)
            {
                IsProfileOperationInProgress = true;
                started = true;
            }
        }).ConfigureAwait(false);
        if (!started)
        {
            return;
        }

        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message)
                ? failureMessage
                : $"{failureMessage}: {ex.Message}";
            Log.Warning(ex, "{FailureMessage}: {Error}", failureMessage, ex.Message);
            await RunOnUiThreadAsync(() => ProfileOperationFailed?.Invoke(this, message)).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiThreadAsync(() => IsProfileOperationInProgress = false).ConfigureAwait(false);
        }
    }

    private void OnProfileChanged(object? sender, ProfileChangedEventArgs e)
    {
        var profile = e.Profile;
        _settingsChanges.InvalidatePendingChanges();
        PostToUiThread(() =>
        {
            RefreshProfileStateCore(profile.Id);
            RefreshProfileSpecificSettingsCore();
        });
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

        _profileManager?.ProfileChanged -= OnProfileChanged;
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
        ObserveTask(OpenGitHubAsync());
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

    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void SynchronizeDraftIfIdle()
    {
        if (Volatile.Read(ref _settingsPersistenceTask)?.IsCompleted is not false)
        {
            _settingsDraft = AppSettingsSnapshot.Copy(_settingsService.Current);
            _lastSubmittedSettings = AppSettingsSnapshot.Copy(_settingsDraft);
        }
    }

    private bool TryPersistSettings(params string[] propertyNames) => TryPersistSettings(onSuccess: null, propertyNames);

    private bool TryPersistSettings(Func<Task>? onSuccess, params string[] propertyNames)
    {
        var request = new SettingsChangeRequest(_lastSubmittedSettings, AppSettingsSnapshot.Copy(_settingsDraft));
        _lastSubmittedSettings = AppSettingsSnapshot.Copy(_settingsDraft);
        var task = PersistSettingsAsync(request, onSuccess, propertyNames);
        Volatile.Write(ref _settingsPersistenceTask, task);
        return onSuccess is null;
    }

    private async Task PersistSettingsAsync(SettingsChangeRequest request, Func<Task>? onSuccess, string[] propertyNames)
    {
        try
        {
            if (onSuccess is null)
            {
                await _settingsChanges.CommitAsync(request, SettingsSaveMode.AfterIdle, CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                await _settingsChanges.CommitWithEffectAsync(request, SettingsSaveMode.AfterIdle,
                    isCurrent => UiDispatcher.InvokeAsync(async () =>
                    {
                        if (isCurrent()) { await onSuccess().ConfigureAwait(false); }
                    }), CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() =>
            {
                RestorePresentationFromSettings();
                foreach (var propertyName in propertyNames) { OnPropertyChanged(propertyName); }
            }).ConfigureAwait(false);
            Log.LogError(error, "Failed to persist settings change");
        }
    }

    private void RestorePresentationFromSettings()
    {
        var current = _settingsService.Current;
        if (!string.Equals(_selectedTheme, current.Theme, StringComparison.Ordinal)
            && !_themeService.TryApplyTheme(current.Theme, out var error))
        {
            Log.Warning("Theme rollback failed: {Error}", error);
            current.Theme = _themeService.CurrentTheme;
        }
        if (!string.Equals(SelectedLogLevel, current.LogLevel, StringComparison.Ordinal)) { _runtimeLogLevelService.SetLogLevel(current.LogLevel); }
        if (!string.Equals(SelectedLanguage, current.Language, StringComparison.Ordinal)) { LocalizationService.SetCulture(current.Language); }
#pragma warning disable MVVMTK0034
        _selectedLogLevel = current.LogLevel;
        _selectedLanguage = current.Language;
#pragma warning restore MVVMTK0034
        _selectedTheme = current.Theme;
        _enableTrayIcon = current.EnableTrayIcon;
        _startMinimized = current.StartMinimized;
        _hideToTrayOnPlayback = current.HideToTrayOnPlayback;
        _hideToTrayOnRecording = current.HideToTrayOnRecording;
        _settingsDraft = AppSettingsSnapshot.Copy(current);
        _lastSubmittedSettings = AppSettingsSnapshot.Copy(current);
        RefreshLanguageOptions();
    }
}
