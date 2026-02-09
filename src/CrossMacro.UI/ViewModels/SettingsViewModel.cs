using System;
using System.Collections.Generic;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
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
        HotkeySettings hotkeySettings)
    {
        _hotkeyService = hotkeyService;
        _settingsService = settingsService;
        _textExpansionService = textExpansionService;
        _hotkeySettings = hotkeySettings;
        
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
        ChangeTheme(_selectedTheme);
        
        // Hide update settings if running as Flatpak
        IsUpdateSettingsVisible = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLATPAK_ID"));

        // Hide tray settings if tray is not supported (Flatpak sandbox blocks D-Bus StatusNotifierItem)
        IsTraySettingsVisible = TrayIconService.IsTraySupported();
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
                _enableTrayIcon = value;
                _settingsService.Current.EnableTrayIcon = value;
                OnPropertyChanged();
                
                // Save settings asynchronously
                _ = _settingsService.SaveAsync();
                
                // Notify for tray icon update
                TrayIconEnabledChanged?.Invoke(this, value);
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
                _settingsService.Current.EnableTextExpansion = value;
                OnPropertyChanged();
                
                // Toggle service immediately
                if (value)
                {
                    System.Threading.Tasks.Task.Run(() => _textExpansionService.Start());
                }
                else
                {
                    System.Threading.Tasks.Task.Run(() => _textExpansionService.Stop());
                }
                
                // Save settings asynchronously
                _ = _settingsService.SaveAsync();
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
                _settingsService.Current.CheckForUpdates = value;
                OnPropertyChanged();
                
                // Save settings asynchronously
                _ = _settingsService.SaveAsync();
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
                _selectedLogLevel = value;
                _settingsService.Current.LogLevel = value;
                OnPropertyChanged();
                
                // Apply log level change immediately at runtime
                LoggerSetup.SetLogLevel(value);
                
                // Save settings asynchronously
                _ = _settingsService.SaveAsync();
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
                _selectedTheme = value;
                _settingsService.Current.Theme = value;
                OnPropertyChanged();
                
                // Apply theme change
                ChangeTheme(value);
                
                // Save settings asynchronously
                _ = _settingsService.SaveAsync();
            }
        }
    }

    public IEnumerable<string> AvailableThemes { get; } = new[]
    {
        "Classic",
        "Latte",
        "Mocha",
        "Dracula",
        "Nord"
    };

    private void ChangeTheme(string theme)
    {
        try
        {
            if (Avalonia.Application.Current?.Resources?.MergedDictionaries is { } dictionaries)
            {
                dictionaries.Clear();
                dictionaries.Add(new Avalonia.Markup.Xaml.Styling.ResourceInclude(new Uri($"avares://CrossMacro.UI/Themes/{theme}.axaml"))
                {
                    Source = new Uri($"avares://CrossMacro.UI/Themes/{theme}.axaml")
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to change theme to {Theme}", theme);
        }
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
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/alper-han/CrossMacro",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open GitHub URL");
        }
    }
}
