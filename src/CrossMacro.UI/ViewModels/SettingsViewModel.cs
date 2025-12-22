using System;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Settings tab - handles hotkey and application settings
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsService _settingsService;
    private readonly HotkeySettings _hotkeySettings;
    
    private string _recordingHotkey;
    private string _playbackHotkey;
    private string _pauseHotkey;
    private bool _enableTrayIcon;
    
    /// <summary>
    /// Event fired when tray icon setting changes
    /// </summary>
    public event EventHandler<bool>? TrayIconEnabledChanged;
    
    public SettingsViewModel(
        IGlobalHotkeyService hotkeyService,
        ISettingsService settingsService,
        HotkeySettings hotkeySettings)
    {
        _hotkeyService = hotkeyService;
        _settingsService = settingsService;
        _hotkeySettings = hotkeySettings;
        
        // Initialize hotkey properties
        _recordingHotkey = _hotkeySettings.RecordingHotkey;
        _playbackHotkey = _hotkeySettings.PlaybackHotkey;
        _pauseHotkey = _hotkeySettings.PauseHotkey;
        
        // Initialize tray icon setting
        _enableTrayIcon = _settingsService.Current.EnableTrayIcon;
    }
    
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
