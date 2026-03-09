using System;
using System.Collections.Generic;
using CrossMacro.Core.Diagnostics;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Logging;
using CrossMacro.Infrastructure.Services;
using CrossMacro.UI.Services;
using Serilog;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Settings tab - handles hotkey and application settings
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsService _settingsService;
    private readonly ITextExpansionService _textExpansionService;
    private readonly HotkeySettings _hotkeySettings;
    private readonly IExternalUrlOpener _externalUrlOpener;
    private readonly IRuntimeContext _runtimeContext;
    private readonly IThemeService _themeService;
    
    private string _recordingHotkey;
    private string _playbackHotkey;
    private string _pauseHotkey;
    private bool _enableTrayIcon;
    private string _selectedLogLevel;
    
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
        IThemeService themeService,
        IRuntimeContext? runtimeContext = null)
    {
        ArgumentNullException.ThrowIfNull(themeService);

        _hotkeyService = hotkeyService;
        _settingsService = settingsService;
        _textExpansionService = textExpansionService;
        _hotkeySettings = hotkeySettings;
        _externalUrlOpener = externalUrlOpener;
        _runtimeContext = runtimeContext ?? new RuntimeContext();
        _themeService = themeService;
        
        // Initialize hotkey properties
        _recordingHotkey = _hotkeySettings.RecordingHotkey;
        _playbackHotkey = _hotkeySettings.PlaybackHotkey;
        _pauseHotkey = _hotkeySettings.PauseHotkey;
        
        // Initialize tray icon setting
        _enableTrayIcon = _settingsService.Current.EnableTrayIcon;
        
        // Initialize log level setting
        _selectedLogLevel = _settingsService.Current.LogLevel;

        // Initialize theme setting
        _selectedTheme = _settingsService.Current.Theme;
        
        // Hide update settings if running as Flatpak
        IsUpdateSettingsVisible = !_runtimeContext.IsFlatpak;

        // Hide tray settings if tray is not supported (Flatpak sandbox blocks D-Bus StatusNotifierItem)
        IsTraySettingsVisible = TrayIconService.IsTraySupported(_runtimeContext);
    }

    public bool IsUpdateSettingsVisible { get; }

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
                var previousValue = _enableTrayIcon;
                _enableTrayIcon = value;
                _settingsService.Current.EnableTrayIcon = value;
                OnPropertyChanged();

                if (TryPersistSettings(
                    () =>
                    {
                        _enableTrayIcon = previousValue;
                        _settingsService.Current.EnableTrayIcon = previousValue;
                    },
                    nameof(EnableTrayIcon)))
                {
                    TrayIconEnabledChanged?.Invoke(this, value);
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
                LoggerSetup.SetLogLevel(value);

                TryPersistSettings(
                    () =>
                    {
                        _selectedLogLevel = previousValue;
                        _settingsService.Current.LogLevel = previousValue;
                        LoggerSetup.SetLogLevel(previousValue);
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
