
namespace CrossMacro.UI.ViewModels;

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
    private int _settingsChangeVersion;

    private bool _enableTrayIcon;
    private bool _startMinimized;
    private bool _disposed;

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
        IManageProfile? manageProfile = null)
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
            manageProfile)
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
        IDirectoryOpener? directoryOpener = null)
    {
        ArgumentNullException.ThrowIfNull(runtimeLogLevelService);
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(runtimeContext);

        GlobalHotkeyService = hotkeyService;
        _settingsService = settingsService;
        _textExpansionService = textExpansionService;
        _hotkeySettings = hotkeySettings;
        _externalUrlOpener = externalUrlOpener;
        _runtimeLogLevelService = runtimeLogLevelService;
        _themeService = themeService;
        LocalizationService = localizationService ?? new LocalizationService();
        _profileManager = profileManager;
        _dialogService = dialogService;
        _manageProfile = manageProfile;
        _themeDirectoryResolver = themeDirectoryResolver;
        _directoryOpener = directoryOpener;

        AvailableProfiles = [];
        _recordingHotkey = _hotkeySettings.RecordingHotkey;
        _playbackHotkey = _hotkeySettings.PlaybackHotkey;
        _pauseHotkey = _hotkeySettings.PauseHotkey;
        _enableTrayIcon = _settingsService.Current.EnableTrayIcon;
        _startMinimized = _settingsService.Current.StartMinimized;
        _selectedLogLevel = _settingsService.Current.LogLevel;
        _selectedTheme = _settingsService.Current.Theme;
        _selectedLanguage = NormalizeSupportedLanguage(_settingsService.Current.Language);
        _settingsService.Current.Language = _selectedLanguage;
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

    // Kept manual: coerces StartMinimized alongside and controls the cross-property notification order.
    public bool EnableTrayIcon
    {
        get => _enableTrayIcon;
        set
        {
            if (_enableTrayIcon != value)
            {
                var previousTrayIcon = _enableTrayIcon;
                var previousStartMinimized = _startMinimized;

                _enableTrayIcon = value;
                _settingsService.Current.EnableTrayIcon = value;

                // Keep persisted startup state coherent: tray-first minimized startup cannot
                // coexist with tray being disabled on supported desktop sessions.
                var startMinimizedStateChanged = false;
                if (!value && IsTraySettingsVisible && _startMinimized)
                {
                    _startMinimized = false;
                    _settingsService.Current.StartMinimized = false;
                    startMinimizedStateChanged = true;
                }

                OnPropertyChanged();
                if (startMinimizedStateChanged)
                {
                    OnPropertyChanged(nameof(StartMinimized));
                }

                var propertyNames = startMinimizedStateChanged
                    ? new[] { nameof(EnableTrayIcon), nameof(StartMinimized) }
                    : [nameof(EnableTrayIcon)];

                _ = TryPersistSettings(
                    () => RestoreStartupPreferences(previousTrayIcon, previousStartMinimized),
                    () =>
                    {
                        TrayIconEnabledChanged?.Invoke(this, _enableTrayIcon);
                        return Task.CompletedTask;
                    },
                    propertyNames);
            }
        }
    }

    // Kept manual: coerces EnableTrayIcon alongside and controls the cross-property notification order.
    public bool StartMinimized
    {
        get => _startMinimized;
        set
        {
            if (_startMinimized != value)
            {
                var previousStartMinimized = _startMinimized;
                var previousTrayIcon = _enableTrayIcon;

                _startMinimized = value;
                _settingsService.Current.StartMinimized = value;

                if (value && IsTraySettingsVisible && !_enableTrayIcon)
                {
                    _enableTrayIcon = true;
                    _settingsService.Current.EnableTrayIcon = true;
                }

                OnPropertyChanged();

                var trayIconStateChanged = previousTrayIcon != _enableTrayIcon;
                if (trayIconStateChanged)
                {
                    OnPropertyChanged(nameof(EnableTrayIcon));
                }

                var propertyNames = trayIconStateChanged
                    ? new[] { nameof(StartMinimized), nameof(EnableTrayIcon) }
                    : [nameof(StartMinimized)];

                _ = TryPersistSettings(
                    () => RestoreStartupPreferences(previousTrayIcon, previousStartMinimized),
                    trayIconStateChanged
                        ? () =>
                        {
                            TrayIconEnabledChanged?.Invoke(this, _enableTrayIcon);
                            return Task.CompletedTask;
                        }
                        : null,
                    propertyNames);
            }
        }
    }


    // Kept manual: no backing field, state proxies ISettingsService directly.
    public bool EnableTextExpansion
    {
        get => _settingsService.Current.EnableTextExpansion;
        set
        {
            if (_settingsService.Current.EnableTextExpansion != value)
            {
                var previousValue = _settingsService.Current.EnableTextExpansion;
                _settingsService.Current.EnableTextExpansion = value;
                OnPropertyChanged();

                _ = TryPersistSettings(
                    () => _settingsService.Current.EnableTextExpansion = previousValue,
                    async () =>
                    {
                        if (_settingsService.Current.EnableTextExpansion)
                        {
                            _textExpansionService.Start();
                        }
                        else
                        {
                            await _textExpansionService.StopExpansionAsync(CancellationToken.None).ConfigureAwait(false);
                        }
                    },
                    nameof(EnableTextExpansion));
            }
        }
    }

    // Kept manual: no backing field, state proxies ISettingsService directly.
    public bool CheckForUpdates
    {
        get => _settingsService.Current.CheckForUpdates;
        set
        {
            if (_settingsService.Current.CheckForUpdates != value)
            {
                var previousValue = _settingsService.Current.CheckForUpdates;
                _settingsService.Current.CheckForUpdates = value;
                OnPropertyChanged();

                _ = TryPersistSettings(
                    () => _settingsService.Current.CheckForUpdates = previousValue,
                    nameof(CheckForUpdates));
            }
        }
    }

    partial void OnSelectedLogLevelChanged(string? oldValue, string newValue)
    {
        var previousValue = oldValue!;
        _settingsService.Current.LogLevel = newValue;
        _runtimeLogLevelService.SetLogLevel(newValue);

        _ = TryPersistSettings(
            () =>
            {
                _selectedLogLevel = previousValue;
                _settingsService.Current.LogLevel = previousValue;
                _runtimeLogLevelService.SetLogLevel(previousValue);
            },
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
        var previousValue = oldValue!;
        _settingsService.Current.Language = newValue;
        LocalizationService.SetCulture(newValue);
        RefreshLanguageOptions();

        _ = TryPersistSettings(
            () =>
            {
                _selectedLanguage = previousValue;
                _settingsService.Current.Language = previousValue;
                LocalizationService.SetCulture(previousValue);
                RefreshLanguageOptions();
            },
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

    // Kept manual: setter rejects the new value entirely when the theme service fails to apply it.
    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (!string.Equals(_selectedTheme, value, StringComparison.Ordinal))
            {
                if (!_themeService.TryApplyTheme(value, out var applyError))
                {
                    Log.Warning("Theme apply failed for '{Theme}': {Error}", value, applyError);
                    return;
                }

                var previousValue = _selectedTheme;
                _selectedTheme = value;
                _settingsService.Current.Theme = value;
                OnPropertyChanged();

                _ = TryPersistSettings(
                    () =>
                    {
                        _selectedTheme = previousValue;
                        _settingsService.Current.Theme = previousValue;
                        if (!_themeService.TryApplyTheme(previousValue, out var revertError))
                        {
                            Log.Warning("Theme rollback failed for '{Theme}': {Error}", previousValue, revertError);
                        }
                    },
                    nameof(SelectedTheme));
            }
        }
    }

    public IEnumerable<string> AvailableThemes => _themeService.AvailableThemes;

    [RelayCommand]
    private void RefreshThemes()
    {
        var previousTheme = _selectedTheme;
        if (!_themeService.TryRefreshThemes(out var refreshError))
        {
            Log.Warning("Theme refresh completed with warnings: {Error}", refreshError);
        }

        OnPropertyChanged(nameof(AvailableThemes));
        if (string.Equals(_selectedTheme, _themeService.CurrentTheme, StringComparison.Ordinal))
        {
            return;
        }

        _selectedTheme = _themeService.CurrentTheme;
        _settingsService.Current.Theme = _selectedTheme;
        OnPropertyChanged(nameof(SelectedTheme));

        _ = TryPersistSettings(
            () =>
            {
                if (!_themeService.TryApplyTheme(previousTheme, out var revertError))
                {
                    Log.Warning("Theme rollback after refresh failed for '{Theme}': {Error}", previousTheme, revertError);
                }

                _selectedTheme = _themeService.CurrentTheme;
                _settingsService.Current.Theme = _selectedTheme;
            },
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
        if ((_profileManager is null && _manageProfile is null) || profileName.Length is 0)
        {
            return;
        }

        await RunProfileOperationAsync(async () =>
        {
            var createdProfile = _manageProfile is not null
                ? (await _manageProfile.CreateAsync(new ProfileRequest(DisplayName: profileName), default).ConfigureAwait(false)).Profile ?? throw new InvalidOperationException("Profile was not returned after creation.")
                : await (_profileManager ?? throw new InvalidOperationException("Profile manager is not initialized.")).CreateProfileAsync(profileName).ConfigureAwait(false);
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
        if ((_profileManager is null && _manageProfile is null) || selectedProfile is null || profileName.Length is 0)
        {
            return;
        }

        await RunProfileOperationAsync(async () =>
        {
            if (_manageProfile is not null)
            {
                _ = await _manageProfile.RenameAsync(new ProfileRequest(selectedProfile.Id, profileName), default).ConfigureAwait(false);
            }
            else
            {
                await (_profileManager ?? throw new InvalidOperationException("Profile manager is not initialized.")).RenameProfileAsync(selectedProfile.Id, profileName).ConfigureAwait(false);
            }
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
        if ((_profileManager is null && _manageProfile is null) || selectedProfile is null)
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
            if (_manageProfile is not null)
            {
                _ = await _manageProfile.DeleteAsync(new ProfileRequest(Identifier: selectedProfile.Id), default).ConfigureAwait(false);
            }
            else
            {
                await (_profileManager ?? throw new InvalidOperationException("Profile manager is not initialized.")).DeleteProfileAsync(selectedProfile.Id).ConfigureAwait(false);
            }
            await RunOnUiThreadAsync(() => RefreshProfileState()).ConfigureAwait(false);
        }, LocalizationService["Settings_ProfileDeleteFailed"]).ConfigureAwait(false);
    }

    public async Task SwitchProfileAsync()
    {
        var selectedProfile = SelectedProfile;
        if ((_profileManager is null && _manageProfile is null) || selectedProfile is null)
        {
            return;
        }

        _ = Interlocked.Increment(ref _settingsChangeVersion);
        await RunProfileOperationAsync(async () =>
        {
            if (_manageProfile is not null)
            {
                _ = await _manageProfile.SwitchAsync(new ProfileRequest(Identifier: selectedProfile.Id), default).ConfigureAwait(false);
            }
            else
            {
                await (_profileManager ?? throw new InvalidOperationException("Profile manager is not initialized.")).SwitchProfileAsync(selectedProfile.Id).ConfigureAwait(false);
            }
            await RunOnUiThreadAsync(() =>
            {
                RefreshProfileState(selectedProfile.Id);
                RefreshProfileSpecificSettings();
            }).ConfigureAwait(false);
        }, LocalizationService["Settings_ProfileSwitchFailed"]).ConfigureAwait(false);
    }

    private void RestoreStartupPreferences(bool trayIconEnabled, bool startMinimized)
    {
        _enableTrayIcon = trayIconEnabled;
        _settingsService.Current.EnableTrayIcon = trayIconEnabled;
        _startMinimized = startMinimized;
        _settingsService.Current.StartMinimized = startMinimized;
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
        _ = Interlocked.Increment(ref _settingsChangeVersion);
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
    /// Start the hotkey service
    /// </summary>
    public void StartHotkeyService()
    {
        try
        {
            GlobalHotkeyService.Start();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (InputBackendErrorClassifier.IsKnownUnavailable(ex))
            {
                Log.Warning("Hotkey service unavailable in current environment: {Error}", ex.Message);
                return;
            }

            Log.LogError(ex, "Hotkey service start error");
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

    private bool TryPersistSettings(Action rollback, params string[] propertyNames)
    {
        return TryPersistSettings(rollback, onSuccess: null, propertyNames);
    }

    private bool TryPersistSettings(Action rollback, Func<Task>? onSuccess, params string[] propertyNames)
    {
        var changeVersion = Interlocked.Increment(ref _settingsChangeVersion);
        _ = TryPersistSettingsAsync(changeVersion, rollback, onSuccess, propertyNames);
        return onSuccess is null;
    }

    private async Task TryPersistSettingsAsync(int changeVersion, Action rollback, Func<Task>? onSuccess, string[] propertyNames)
    {
        try
        {
            await _settingsService.SaveAsync().ConfigureAwait(false);
            if (onSuccess is not null)
            {
                await onSuccess().ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (Volatile.Read(ref _settingsChangeVersion) == changeVersion)
            {
                await RunOnUiThreadAsync(() =>
                {
                    rollback();
                    foreach (var propertyName in propertyNames)
                    {
                        OnPropertyChanged(propertyName);
                    }
                }).ConfigureAwait(false);
            }

            Log.LogError(ex, "Failed to persist settings change");
        }
    }
}
