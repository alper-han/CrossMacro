using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CrossMacro.Core.Diagnostics;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Settings tab - handles hotkey and application settings
/// </summary>
public class SettingsViewModel : ViewModelBase, IDisposable
{
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
        new("tr", "Language_Turkish", "Turkish")
    ];

    internal static IReadOnlyList<string> SupportedLanguageCodes { get; } = SupportedLanguages
        .Select(language => language.Code)
        .ToArray();

    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsService _settingsService;
    private readonly ITextExpansionService _textExpansionService;
    private readonly HotkeySettings _hotkeySettings;
    private readonly IExternalUrlOpener _externalUrlOpener;
    private readonly IRuntimeContext _runtimeContext;
    private readonly IRuntimeLogLevelService _runtimeLogLevelService;
    private readonly IThemeService _themeService;
    private readonly ILocalizationService _localizationService;
    private readonly IProfileManager? _profileManager;
    private readonly IDialogService? _dialogService;
    
    private string _recordingHotkey;
    private string _playbackHotkey;
    private string _pauseHotkey;
    private bool _enableTrayIcon;
    private bool _startMinimized;
    private string _selectedLogLevel;
    private string _selectedLanguage;
    private IReadOnlyList<LanguageOption> _availableLanguages;
    private IReadOnlyList<ProfileInfo> _availableProfiles = [];
    private ProfileInfo? _selectedProfile;
    private bool _isProfileOperationInProgress;
    private string _newProfileName = string.Empty;
    private bool _disposed;
    
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
        IDialogService? dialogService = null)
    {
        ArgumentNullException.ThrowIfNull(runtimeLogLevelService);
        ArgumentNullException.ThrowIfNull(themeService);

        _hotkeyService = hotkeyService;
        _settingsService = settingsService;
        _textExpansionService = textExpansionService;
        _hotkeySettings = hotkeySettings;
        _externalUrlOpener = externalUrlOpener;
        _runtimeContext = runtimeContext ?? new RuntimeContext();
        _runtimeLogLevelService = runtimeLogLevelService;
        _themeService = themeService;
        _localizationService = localizationService ?? new LocalizationService();
        _profileManager = profileManager;
        _dialogService = dialogService;
        
        // Initialize hotkey properties
        _recordingHotkey = _hotkeySettings.RecordingHotkey;
        _playbackHotkey = _hotkeySettings.PlaybackHotkey;
        _pauseHotkey = _hotkeySettings.PauseHotkey;
        
        // Initialize tray icon setting
        _enableTrayIcon = _settingsService.Current.EnableTrayIcon;
        _startMinimized = _settingsService.Current.StartMinimized;
        
        // Initialize log level setting
        _selectedLogLevel = _settingsService.Current.LogLevel;

        // Initialize theme setting
        _selectedTheme = _settingsService.Current.Theme;

        _selectedLanguage = NormalizeSupportedLanguage(_settingsService.Current.Language);
        _settingsService.Current.Language = _selectedLanguage;
        _availableLanguages = CreateLanguageOptions();
        RefreshLanguageOptions();
        
        // Hide update settings if running as Flatpak
        IsUpdateSettingsVisible = !_runtimeContext.IsFlatpak;

        // Hide tray settings if tray is not supported (Flatpak sandbox blocks D-Bus StatusNotifierItem)
        IsTraySettingsVisible = TrayIconService.IsTraySupported(_runtimeContext);

        RefreshProfileState();
        if (_profileManager != null)
        {
            _profileManager.ProfileChanged += OnProfileChanged;
        }
    }

    public SettingsViewModel(
        IGlobalHotkeyService hotkeyService,
        ISettingsService settingsService,
        ITextExpansionService textExpansionService,
        HotkeySettings hotkeySettings,
        IExternalUrlOpener externalUrlOpener,
        IRuntimeLogLevelService runtimeLogLevelService,
        IThemeService themeService,
        IRuntimeContext runtimeContext)
        : this(
            hotkeyService,
            settingsService,
            textExpansionService,
            hotkeySettings,
            externalUrlOpener,
            runtimeLogLevelService,
            themeService,
            null,
            runtimeContext)
    {
    }

    public bool IsUpdateSettingsVisible { get; }

    public IGlobalHotkeyService GlobalHotkeyService => _hotkeyService;

    public ILocalizationService LocalizationService => _localizationService;

    public IReadOnlyList<ProfileInfo> AvailableProfiles
    {
        get => _availableProfiles;
        private set => SetProperty(ref _availableProfiles, value);
    }

    public ProfileInfo? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public bool IsProfileOperationInProgress
    {
        get => _isProfileOperationInProgress;
        private set => SetProperty(ref _isProfileOperationInProgress, value);
    }

    public string NewProfileName
    {
        get => _newProfileName;
        set => SetProperty(ref _newProfileName, value);
    }

    /// <summary>
    /// Tray icon settings are hidden in Flatpak where StatusNotifierItem is not supported
    /// </summary>
    public bool IsTraySettingsVisible { get; }
    
    public string RecordingHotkey
    {
        get => _recordingHotkey;
        set
        {
            if (_recordingHotkey != value)
            {
                _recordingHotkey = value;
                _hotkeySettings.RecordingHotkey = value;
                OnPropertyChanged();
                UpdateHotkeys();
            }
        }
    }
    
    public string PlaybackHotkey
    {
        get => _playbackHotkey;
        set
        {
            if (_playbackHotkey != value)
            {
                _playbackHotkey = value;
                _hotkeySettings.PlaybackHotkey = value;
                OnPropertyChanged();
                UpdateHotkeys();
            }
        }
    }
    
    public string PauseHotkey
    {
        get => _pauseHotkey;
        set
        {
            if (_pauseHotkey != value)
            {
                _pauseHotkey = value;
                _hotkeySettings.PauseHotkey = value;
                OnPropertyChanged();
                UpdateHotkeys();
            }
        }
    }
    
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
                    : new[] { nameof(EnableTrayIcon) };

                if (TryPersistSettings(
                    () => RestoreStartupPreferences(previousTrayIcon, previousStartMinimized),
                    propertyNames))
                {
                    TrayIconEnabledChanged?.Invoke(this, _enableTrayIcon);
                }
            }
        }
    }

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
                    : new[] { nameof(StartMinimized) };

                if (TryPersistSettings(
                    () => RestoreStartupPreferences(previousTrayIcon, previousStartMinimized),
                    propertyNames))
                {
                    if (trayIconStateChanged)
                    {
                        TrayIconEnabledChanged?.Invoke(this, _enableTrayIcon);
                    }
                }
            }
        }
    }
    
    
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

                if (TryPersistSettings(
                    () => _settingsService.Current.EnableTextExpansion = previousValue,
                    nameof(EnableTextExpansion)))
                {
                    if (value)
                    {
                        _textExpansionService.Start();
                    }
                    else
                    {
                        _textExpansionService.Stop();
                    }
                }
            }
        }
    }

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

                TryPersistSettings(
                    () => _settingsService.Current.CheckForUpdates = previousValue,
                    nameof(CheckForUpdates));
            }
        }
    }
    
    /// <summary>
    /// Selected log level for the application
    /// </summary>
    public string SelectedLogLevel
    {
        get => _selectedLogLevel;
        set
        {
            if (_selectedLogLevel != value)
            {
                var previousValue = _selectedLogLevel;
                _selectedLogLevel = value;
                _settingsService.Current.LogLevel = value;
                OnPropertyChanged();
                _runtimeLogLevelService.SetLogLevel(value);

                TryPersistSettings(
                    () =>
                    {
                        _selectedLogLevel = previousValue;
                        _settingsService.Current.LogLevel = previousValue;
                        _runtimeLogLevelService.SetLogLevel(previousValue);
                    },
                    nameof(SelectedLogLevel));
            }
        }
    }
    
    /// <summary>
    /// Available log levels for the ComboBox
    /// </summary>
    public IEnumerable<string> LogLevels { get; } = new[]
    {
        "Debug",
        "Information",
        "Warning",
        "Error"
    };

    public IReadOnlyList<LanguageOption> AvailableLanguages => _availableLanguages;

    public LanguageOption? SelectedLanguageOption
    {
        get => _availableLanguages.FirstOrDefault(option => option.Code == _selectedLanguage);
        set
        {
            if (value is null)
            {
                return;
            }

            SelectedLanguage = value.Code;
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage != value)
            {
                var previousValue = _selectedLanguage;
                _selectedLanguage = value;
                _settingsService.Current.Language = value;
                _localizationService.SetCulture(value);
                RefreshLanguageOptions();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedLanguageOption));

                TryPersistSettings(
                    () =>
                    {
                        _selectedLanguage = previousValue;
                        _settingsService.Current.Language = previousValue;
                        _localizationService.SetCulture(previousValue);
                        RefreshLanguageOptions();
                    },
                    nameof(SelectedLanguage),
                    nameof(AvailableLanguages),
                    nameof(SelectedLanguageOption));
            }
        }
    }

    private void RefreshLanguageOptions()
    {
        foreach (var option in _availableLanguages)
        {
            option.DisplayName = GetLanguageDisplayName(option.Code);
        }

        OnPropertyChanged(nameof(AvailableLanguages));
        OnPropertyChanged(nameof(SelectedLanguageOption));
    }

    private IReadOnlyList<LanguageOption> CreateLanguageOptions()
    {
        return SupportedLanguages
            .OrderByDescending(language => language.IsDefault)
            .ThenBy(language => language.EnglishName, StringComparer.Ordinal)
            .Select(language => new LanguageOption
            {
                Code = language.Code,
                DisplayName = GetLanguageDisplayName(language)
            })
            .ToArray();
    }

    private string GetLanguageDisplayName(string code)
    {
        var language = SupportedLanguages.FirstOrDefault(language =>
            string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase));
        return language is null
            ? _localizationService[SupportedLanguages[0].ResourceKey]
            : GetLanguageDisplayName(language);
    }

    private string GetLanguageDisplayName(SupportedLanguageDescriptor language)
    {
        return _localizationService[language.ResourceKey];
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

        return SupportedLanguageCodes.Contains(language, StringComparer.OrdinalIgnoreCase)
            ? language.ToLowerInvariant()
            : "en";
    }

    private string _selectedTheme;
    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme != value)
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

                TryPersistSettings(
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

    public async Task CreateProfileAsync()
    {
        var profileName = NewProfileName.Trim();
        if (_profileManager is null || profileName.Length == 0)
        {
            return;
        }

        await RunProfileOperationAsync(async () =>
        {
            var createdProfile = await _profileManager.CreateProfileAsync(profileName);
            RefreshProfileState(createdProfile.Id);
            NewProfileName = string.Empty;
        }, _localizationService["Settings_ProfileCreateFailed"]);
    }

    public Task CreateProfile() => CreateProfileAsync();

    public async Task RenameSelectedProfileAsync()
    {
        var profileName = NewProfileName.Trim();
        var selectedProfile = SelectedProfile;
        if (_profileManager is null || selectedProfile is null || profileName.Length == 0)
        {
            return;
        }

        await RunProfileOperationAsync(async () =>
        {
            await _profileManager.RenameProfileAsync(selectedProfile.Id, profileName);
            RefreshProfileState(selectedProfile.Id);
            NewProfileName = string.Empty;
        }, _localizationService["Settings_ProfileRenameFailed"]);
    }

    public Task RenameSelectedProfile() => RenameSelectedProfileAsync();

    public async Task DeleteSelectedProfileAsync()
    {
        var selectedProfile = SelectedProfile;
        if (_profileManager is null || selectedProfile is null)
        {
            return;
        }

        if (IsProfileOperationInProgress)
        {
            return;
        }

        if (_dialogService != null)
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                _localizationService["Settings_ProfileDeleteTitle"],
                string.Format(
                    _localizationService.CurrentCulture,
                    _localizationService["Settings_ProfileDeleteMessage"],
                    selectedProfile.Name));

            if (!confirmed)
            {
                return;
            }
        }

        await RunProfileOperationAsync(async () =>
        {
            await _profileManager.DeleteProfileAsync(selectedProfile.Id);
            RefreshProfileState();
        }, _localizationService["Settings_ProfileDeleteFailed"]);
    }

    public Task DeleteSelectedProfile() => DeleteSelectedProfileAsync();

    public async Task SwitchProfileAsync()
    {
        var selectedProfile = SelectedProfile;
        if (_profileManager is null || selectedProfile is null)
        {
            return;
        }

        await RunProfileOperationAsync(async () =>
        {
            await _profileManager.SwitchProfileAsync(selectedProfile.Id);
            RefreshProfileState(selectedProfile.Id);
            RefreshProfileSpecificSettings();
        }, _localizationService["Settings_ProfileSwitchFailed"]);
    }

    public Task SwitchProfile() => SwitchProfileAsync();

    private void RestoreStartupPreferences(bool trayIconEnabled, bool startMinimized)
    {
        _enableTrayIcon = trayIconEnabled;
        _settingsService.Current.EnableTrayIcon = trayIconEnabled;
        _startMinimized = startMinimized;
        _settingsService.Current.StartMinimized = startMinimized;
    }

    public void RefreshProfileState(string? selectedProfileId = null)
    {
        void Refresh()
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

        RaiseOnUiThread(Refresh);
    }

    public void RefreshProfileSpecificSettings()
    {
        void Refresh()
        {
            _recordingHotkey = _hotkeySettings.RecordingHotkey;
            _playbackHotkey = _hotkeySettings.PlaybackHotkey;
            _pauseHotkey = _hotkeySettings.PauseHotkey;

            OnPropertyChanged(nameof(RecordingHotkey));
            OnPropertyChanged(nameof(PlaybackHotkey));
            OnPropertyChanged(nameof(PauseHotkey));
            OnPropertyChanged(nameof(EnableTextExpansion));
            OnPropertyChanged(nameof(CheckForUpdates));
        }

        RaiseOnUiThread(Refresh);
    }

    private async Task RunProfileOperationAsync(Func<Task> operation, string failureMessage)
    {
        if (IsProfileOperationInProgress)
        {
            return;
        }

        try
        {
            IsProfileOperationInProgress = true;
            await operation();
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message)
                ? failureMessage
                : $"{failureMessage}: {ex.Message}";
            Log.Warning(ex, "{FailureMessage}: {Error}", failureMessage, ex.Message);
            RaiseOnUiThread(() => ProfileOperationFailed?.Invoke(this, message));
        }
        finally
        {
            IsProfileOperationInProgress = false;
        }
    }

    private void OnProfileChanged(object? sender, ProfileInfo profile)
    {
        RefreshProfileState(profile.Id);
        RefreshProfileSpecificSettings();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_profileManager != null)
        {
            _profileManager.ProfileChanged -= OnProfileChanged;
        }
    }

    private static void RaiseOnUiThread(Action action)
    {
        if (Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    private void UpdateHotkeys()
    {
        try
        {
            if (_hotkeyService.IsRunning)
            {
                _hotkeyService.UpdateHotkeys(
                    _hotkeySettings.RecordingHotkey,
                    _hotkeySettings.PlaybackHotkey,
                    _hotkeySettings.PauseHotkey);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Hotkey update error");
        }
    }
    
    /// <summary>
    /// Start the hotkey service
    /// </summary>
    public void StartHotkeyService()
    {
        try
        {
            _hotkeyService.Start();
        }
        catch (Exception ex)
        {
            if (InputBackendErrorClassifier.IsKnownUnavailable(ex))
            {
                Log.Warning("Hotkey service unavailable in current environment: {Error}", ex.Message);
                return;
            }

            Log.Error(ex, "Hotkey service start error");
        }
    }
    /// <summary>
    /// Open the GitHub repository
    /// </summary>
    public void OpenGitHub()
    {
        try
        {
            _externalUrlOpener.Open("https://github.com/alper-han/CrossMacro");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open GitHub URL");
        }
    }

    private bool TryPersistSettings(Action rollback, params string[] propertyNames)
    {
        try
        {
            _settingsService.Save();
            return true;
        }
        catch (Exception ex)
        {
            rollback();
            foreach (var propertyName in propertyNames)
            {
                OnPropertyChanged(propertyName);
            }

            Log.Error(ex, "Failed to persist settings change");
            return false;
        }
    }
}
