using System;
using Avalonia.Threading;
using Avalonia.Controls.Notifications;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Wayland;
using CrossMacro.Core.Wayland;
using CrossMacro.Infrastructure.Helpers;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// Coordinator ViewModel - manages child ViewModels and cross-cutting concerns
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IMousePositionProvider _positionProvider;
    
    private string? _extensionWarning;
    private bool _hasExtensionWarning;
    
    // Warning sources
    private string? _gnomeWarning;
    private string? _uinputWarning;

    private string _globalStatus = "Ready";
    
    public WindowNotificationManager? NotificationManager { get; set; }
    
    // Child ViewModels
    public RecordingViewModel Recording { get; }
    public PlaybackViewModel Playback { get; }
    public FilesViewModel Files { get; }
    public TextExpansionViewModel TextExpansion { get; }
    public SettingsViewModel Settings { get; }
    
    public bool IsCloseButtonVisible { get; }
    
    /// <summary>
    /// Event fired when tray icon setting changes (for App.axaml.cs)
    /// </summary>
    public event EventHandler<bool>? TrayIconEnabledChanged;
    
    public MainWindowViewModel(
        RecordingViewModel recording,
        PlaybackViewModel playback,
        FilesViewModel files,
        TextExpansionViewModel textExpansion,
        SettingsViewModel settings,
        IGlobalHotkeyService hotkeyService,
        IMousePositionProvider positionProvider)
    {
        Recording = recording;
        Playback = playback;
        Files = files;
        TextExpansion = textExpansion;
        Settings = settings;
        _hotkeyService = hotkeyService;
        _positionProvider = positionProvider;
        
        // Hide close button on Hyprland
        var compositor = CompositorDetector.DetectCompositor();
        IsCloseButtonVisible = compositor != CompositorType.HYPRLAND;
        
        // Wire up cross-ViewModel communication
        SetupViewModelCommunication();
        
        // Subscribe to hotkey events
        _hotkeyService.ToggleRecordingRequested += OnToggleRecordingRequested;
        _hotkeyService.TogglePlaybackRequested += OnTogglePlaybackRequested;
        _hotkeyService.TogglePauseRequested += OnTogglePauseRequested;
        
        // Subscribe to extension status events
        SetupExtensionStatusHandling();
        
        // Forward tray icon changes
        Settings.TrayIconEnabledChanged += (s, enabled) => TrayIconEnabledChanged?.Invoke(this, enabled);
        
        // Start hotkey service
        Settings.StartHotkeyService();
        
        // Check permissions
        CheckPermissions();
    }
    
    private void SetupViewModelCommunication()
    {
        // When recording completes, update Files and Playback
        Recording.RecordingCompleted += (s, macro) =>
        {
            Files.SetMacro(macro);
            Playback.SetMacro(macro);
            GlobalStatus = $"Recorded {macro.EventCount} events";
        };
        
        // When recording state changes, update Playback's ability to start
        Recording.RecordingStateChanged += (s, isRecording) =>
        {
            Playback.CanPlayMacroExternal = !isRecording;
        };
        
        // When playback state changes, update Recording's ability to start
        Playback.PlaybackStateChanged += (s, isPlaying) =>
        {
            Recording.CanStartRecordingExternal = !isPlaying;
        };
        
        // When a macro is loaded, update Playback
        Files.MacroLoaded += (s, macro) =>
        {
            Playback.SetMacro(macro);
            GlobalStatus = $"Loaded: {macro.Name}";
        };
        
        // Forward status changes
        Recording.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Recording.RecordingStatus))
                GlobalStatus = Recording.RecordingStatus;
        };
        
        Playback.StatusChanged += (s, status) => GlobalStatus = status;
        Files.StatusChanged += (s, status) => GlobalStatus = status;
    }
    
    private void SetupExtensionStatusHandling()
    {
        if (_positionProvider is GnomePositionProvider gnomeProvider)
        {
            gnomeProvider.ExtensionStatusChanged += OnExtensionStatusChanged;
        }
        
        if (_positionProvider is KdePositionProvider kdeProvider)
        {
            kdeProvider.ExtensionStatusChanged += OnExtensionStatusChanged;
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
    
    public string GlobalStatus
    {
        get => _globalStatus;
        set
        {
            if (_globalStatus != value)
            {
                _globalStatus = value;
                OnPropertyChanged();
            }
        }
    }
    
    private void OnExtensionStatusChanged(object? sender, string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (message.Contains("enabled successfully"))
            {
                NotificationManager?.Show(new Notification(
                    "GNOME Extension", 
                    message, 
                    NotificationType.Success,
                    TimeSpan.FromSeconds(3)));
                
                // Clear warning if it was set
                if (_gnomeWarning != null)
                {
                    _gnomeWarning = null;
                    UpdateCombinedWarning();
                }
                return;
            }
            
            _gnomeWarning = message;
            UpdateCombinedWarning();
        });
    }

    private void CheckPermissions()
    {
        Task.Run(() => 
        {
            if (OperatingSystem.IsLinux() && !PermissionHelper.CheckUInputAccess())
            {
                Dispatcher.UIThread.Post(() => {
                    _uinputWarning = "⚠️ Missing Permissions: You are not in the 'input' group.";
                    UpdateCombinedWarning();
                });
            }
        });
    }

    private void UpdateCombinedWarning()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(_uinputWarning)) parts.Add(_uinputWarning);
        if (!string.IsNullOrEmpty(_gnomeWarning)) parts.Add(_gnomeWarning);

        if (parts.Count > 0)
        {
            ExtensionWarning = string.Join("\n\n", parts);
            HasExtensionWarning = true;
        }
        else
        {
            ExtensionWarning = null;
            HasExtensionWarning = false;
        }
    }
    
    private void OnToggleRecordingRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Recording.ToggleRecording();
        });
    }

    private void OnTogglePlaybackRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Playback.TogglePlayback();
        });
    }

    private void OnTogglePauseRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Playback.TogglePause();
        });
    }
}
