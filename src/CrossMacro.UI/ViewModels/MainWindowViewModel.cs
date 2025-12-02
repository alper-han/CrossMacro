using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Controls.Notifications;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Native.Evdev;
using CrossMacro.Infrastructure.Wayland;
using CrossMacro.Core.Wayland;

namespace CrossMacro.UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IMacroRecorder _recorder;
    private readonly IMacroPlayer _player;
    private readonly IMacroFileManager _fileManager;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IMousePositionProvider _positionProvider;
    private readonly HotkeySettings _hotkeySettings;
    private readonly ISettingsService _settingsService;
    
    public WindowNotificationManager? NotificationManager { get; set; }
    
    private bool _isRecording;
    private int _eventCount;
    private string _recordingStatus = "Ready";
    private bool _hasRecordedMacro;
    private double _playbackSpeed = 1.0;
    private bool _isLooping;
    private int _loopCount = 1;
    private int _loopDelayMs = 0;
    private MacroSequence? _currentMacro;
    private bool _isPlaying;
    private string _macroName = "New Macro";
    private int _countdownSeconds;
    private string? _extensionWarning;
    private bool _hasExtensionWarning;
    
    private bool _isPaused;
    
    // Hotkey settings
    private string _recordingHotkey;
    private string _playbackHotkey;
    private string _pauseHotkey;
    
    // Tray settings
    private bool _enableTrayIcon;

    public bool IsCloseButtonVisible { get; }

    public MainWindowViewModel(
        IMacroRecorder recorder,
        IMacroPlayer player,
        IMacroFileManager fileManager,
        IGlobalHotkeyService hotkeyService,
        IMousePositionProvider positionProvider,
        HotkeySettings hotkeySettings,
        ISettingsService settingsService)
    {
        _recorder = recorder;
        _player = player;
        _fileManager = fileManager;
        _hotkeyService = hotkeyService;
        _positionProvider = positionProvider;
        _hotkeySettings = hotkeySettings;
        _settingsService = settingsService;
        
        // Initialize hotkey properties
        _recordingHotkey = _hotkeySettings.RecordingHotkey;
        _playbackHotkey = _hotkeySettings.PlaybackHotkey;
        _pauseHotkey = _hotkeySettings.PauseHotkey;
        
        // Initialize tray icon setting
        _enableTrayIcon = _settingsService.Current.EnableTrayIcon;

        // Hide close button on Hyprland
        var compositor = CompositorDetector.DetectCompositor();
        IsCloseButtonVisible = compositor != CompositorType.HYPRLAND;
        
        // Subscribe to recording events
        _recorder.EventRecorded += OnEventRecorded;
        
        // Subscribe to hotkey events
        _hotkeyService.ToggleRecordingRequested += OnToggleRecordingRequested;
        _hotkeyService.TogglePlaybackRequested += OnTogglePlaybackRequested;
        _hotkeyService.TogglePauseRequested += OnTogglePauseRequested;
        
        // Subscribe to GNOME extension status events if using GnomePositionProvider
        if (_positionProvider is GnomePositionProvider gnomeProvider)
        {
            gnomeProvider.ExtensionStatusChanged += OnExtensionStatusChanged;
        }
        
        // Start hotkey service automatically
        StartHotkeyService();
    }
    
    // ... (Previous code remains) ...

    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            if (_isRecording != value)
            {
                _isRecording = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanPlayMacro));
                RecordingStatus = value ? "Recording..." : "Ready";
            }
        }
    }
    
    public int EventCount
    {
        get => _eventCount;
        set
        {
            if (_eventCount != value)
            {
                _eventCount = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string RecordingStatus
    {
        get => _recordingStatus;
        set
        {
            if (_recordingStatus != value)
            {
                _recordingStatus = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool HasRecordedMacro
    {
        get => _hasRecordedMacro;
        set
        {
            if (_hasRecordedMacro != value)
            {
                _hasRecordedMacro = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanPlayMacro));
            }
        }
    }

    public bool CanPlayMacro => HasRecordedMacro && !IsRecording;
    
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            if (Math.Abs(_playbackSpeed - value) > 0.01)
            {
                _playbackSpeed = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool IsLooping
    {
        get => _isLooping;
        set
        {
            if (_isLooping != value)
            {
                _isLooping = value;
                OnPropertyChanged();
            }
        }
    }
    
    public int LoopCount
    {
        get => _loopCount;
        set
        {
            if (_loopCount != value)
            {
                _loopCount = value;
                OnPropertyChanged();
            }
        }
    }
    
    public int LoopDelayMs
    {
        get => _loopDelayMs;
        set
        {
            if (_loopDelayMs != value)
            {
                _loopDelayMs = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string MacroName
    {
        get => _macroName;
        set
        {
            if (_macroName != value)
            {
                _macroName = value;
                OnPropertyChanged();
            }
        }
    }
    
    public int CountdownSeconds
    {
        get => _countdownSeconds;
        set
        {
            if (_countdownSeconds != value)
            {
                _countdownSeconds = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string? ExtensionWarning
    {
        get => _extensionWarning;
        set
        {
            if (_extensionWarning != value)
            {
                _extensionWarning = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool HasExtensionWarning
    {
        get => _hasExtensionWarning;
        set
        {
            if (_hasExtensionWarning != value)
            {
                _hasExtensionWarning = value;
                OnPropertyChanged();
            }
        }
    }



    private void OnEventRecorded(object? sender, MacroEvent e)
    {
        // Update UI on main thread
        Dispatcher.UIThread.Post(() => {
            EventCount++;
        });
    }

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (_isPaused != value)
            {
                _isPaused = value;
                OnPropertyChanged();
            }
        }
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
                
                // Notify App.axaml.cs to update tray icon
                TrayIconEnabledChanged?.Invoke(this, value);
            }
        }
    }
    
    public event EventHandler<bool>? TrayIconEnabledChanged;
    
    private void OnExtensionStatusChanged(object? sender, string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // If it's a success message, show temporary notification
            if (message.Contains("enabled successfully"))
            {
                NotificationManager?.Show(new Notification(
                    "GNOME Extension", 
                    message, 
                    NotificationType.Success,
                    TimeSpan.FromSeconds(3)));
                return;
            }
            
            // If it's an error/warning, only show persistent warning banner (no popup notification)
            ExtensionWarning = message;
            HasExtensionWarning = true;
        });
    }



    private void OnToggleRecordingRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsRecording)
                StopRecording();
            else
                StartRecording();
        });
    }

    private void OnTogglePlaybackRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsPlaying)
                StopPlayback();
            else
                PlayMacro();
        });
    }

    private void OnTogglePauseRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsPlaying) return;

            if (_player.IsPaused)
            {
                _player.Resume();
                IsPaused = false;
                RecordingStatus = "Resumed";
            }
            else
            {
                _player.Pause();
                IsPaused = true;
                RecordingStatus = "Paused";
            }
        });
    }


    
    public async void StartRecording()
    {
        try
        {
            IsRecording = true;
            EventCount = 0;
            
            // Disable playback and pause hotkeys during recording so they can be recorded
            _hotkeyService.SetPlaybackPauseHotkeysEnabled(false);
            
            await _recorder.StartRecordingAsync();
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Error: {ex.Message}";
            IsRecording = false;
            
            // Re-enable hotkeys on error
            _hotkeyService.SetPlaybackPauseHotkeysEnabled(true);
        }
    }
    
    public void StopRecording()
    {
        try
        {
            _currentMacro = _recorder.StopRecording();
            IsRecording = false;
            HasRecordedMacro = _currentMacro != null && _currentMacro.EventCount > 0;
            
            if (HasRecordedMacro)
            {
                _currentMacro!.Name = MacroName;
                RecordingStatus = $"Recorded {_currentMacro!.EventCount} events";
            }
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Error: {ex.Message}";
            IsRecording = false;
        }
        finally
        {
            // Re-enable playback and pause hotkeys after recording stops
            _hotkeyService.SetPlaybackPauseHotkeysEnabled(true);
        }
    }
    
    public async void PlayMacro()
    {
        if (_currentMacro == null || IsPlaying)
            return;
        
        try
        {
            IsPlaying = true;
            
            // Countdown
            if (CountdownSeconds > 0)
            {
                for (int i = CountdownSeconds; i > 0; i--)
                {
                    RecordingStatus = $"Starting in {i}...";
                    await Task.Delay(1000);
                    if (!IsPlaying) return; // Cancelled via Stop
                }
            }
            
            RecordingStatus = "Playing...";
            
            var options = new PlaybackOptions
            {
                SpeedMultiplier = PlaybackSpeed,
                Loop = IsLooping,
                RepeatCount = LoopCount,
                RepeatDelayMs = LoopDelayMs
            };
            
            await _player.PlayAsync(_currentMacro, options);
            
            RecordingStatus = "Playback complete";
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Playback error: {ex.Message}";
        }
        finally
        {
            IsPlaying = false;
        }
    }
    
    public void StopPlayback()
    {
        if (IsPlaying)
        {
            IsPlaying = false;
            _player.Stop();
            RecordingStatus = "Playback stopped";
        }
    }
    
    public async void SaveMacro()
    {
        if (_currentMacro == null)
            return;
        
        try
        {
            var mainWindow = (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow == null)
            {
                RecordingStatus = "Error: Cannot open file dialog";
                return;
            }

            var dialog = new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save Macro",
                SuggestedFileName = $"{MacroName}.macro",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("Macro Files")
                    {
                        Patterns = new[] { "*.macro" }
                    }
                }
            };

            var result = await mainWindow.StorageProvider.SaveFilePickerAsync(dialog);
            if (result == null)
            {
                RecordingStatus = "Save cancelled";
                return;
            }

            var filePath = result.Path.LocalPath;
            _currentMacro.Name = MacroName;
            await _fileManager.SaveAsync(_currentMacro, filePath);
            
            RecordingStatus = $"Saved to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Save error: {ex.Message}";
        }
    }
    
    public async void LoadMacro()
    {
        try
        {
            var mainWindow = (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow == null)
            {
                RecordingStatus = "Error: Cannot open file dialog";
                return;
            }

            var dialog = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Load Macro",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("Macro Files")
                    {
                        Patterns = new[] { "*.macro" }
                    }
                }
            };

            var result = await mainWindow.StorageProvider.OpenFilePickerAsync(dialog);
            if (result == null || result.Count == 0)
            {
                RecordingStatus = "Load cancelled";
                return;
            }

            var filePath = result[0].Path.LocalPath;
            _currentMacro = await _fileManager.LoadAsync(filePath);
            
            if (_currentMacro != null)
            {
                HasRecordedMacro = true;
                EventCount = _currentMacro.EventCount;
                MacroName = _currentMacro.Name;
                RecordingStatus = $"Loaded {Path.GetFileName(filePath)}";
            }
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Load error: {ex.Message}";
        }
    }

    private void StartHotkeyService()
    {
        try
        {
            _hotkeyService.Start();
            RecordingStatus = "Hotkeys active";
        }
        catch (Exception ex)
        {
            RecordingStatus = $"Hotkey error: {ex.Message}";
            Console.WriteLine(ex);
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
            RecordingStatus = $"Hotkey update error: {ex.Message}";
        }
    }
    

}
