using System;
using Avalonia.Threading;
using Avalonia.Controls.Notifications;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Wayland;
using CrossMacro.UI.Models;
using System.Collections.ObjectModel;
using System.Linq;

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

    private string _globalStatus = "Ready";
    
    public WindowNotificationManager? NotificationManager { get; set; }
    
    // Child ViewModels
    public RecordingViewModel Recording { get; }
    public PlaybackViewModel Playback { get; }
    public FilesViewModel Files { get; }
    public TextExpansionViewModel TextExpansion { get; }
    public ScheduleViewModel Schedule { get; }
    public ShortcutViewModel Shortcuts { get; }
    public SettingsViewModel Settings { get; }
    
    
    public bool IsCloseButtonVisible { get; }

    private bool _isPaneOpen = true;
    public bool IsPaneOpen
    {
        get => _isPaneOpen;
        set
        {
            if (_isPaneOpen != value)
            {
                _isPaneOpen = value;
                OnPropertyChanged();
            }
        }
    }

    private NavigationItem? _selectedTopItem;
    public NavigationItem? SelectedTopItem
    {
        get => _selectedTopItem;
        set
        {
            if (_selectedTopItem != value)
            {
                _selectedTopItem = value;
                OnPropertyChanged();
                
                if (value != null)
                {
                    SelectedBottomItem = null;
                    SelectedNavigationItem = value;
                }
            }
        }
    }

    private NavigationItem? _selectedBottomItem;
    public NavigationItem? SelectedBottomItem
    {
        get => _selectedBottomItem;
        set
        {
            if (_selectedBottomItem != value)
            {
                _selectedBottomItem = value;
                OnPropertyChanged();
                
                if (value != null)
                {
                    SelectedTopItem = null;
                    SelectedNavigationItem = value;
                }
            }
        }
    }

    private NavigationItem? _selectedNavigationItem;
    public NavigationItem? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        private set
        {
            if (_selectedNavigationItem != value)
            {
                _selectedNavigationItem = value;
                OnPropertyChanged();
                if (value != null)
                {
                    CurrentPage = value.ViewModel;
                }
            }
        }
    }

    private ViewModelBase? _currentPage;
    public ViewModelBase? CurrentPage
    {
        get => _currentPage;
        set
        {
            if (_currentPage != value)
            {
                _currentPage = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<NavigationItem> TopNavigationItems { get; private set; }
    public ObservableCollection<NavigationItem> BottomNavigationItems { get; private set; }

    
    /// <summary>
    /// Application version from assembly
    /// </summary>
    public string AppVersion { get; } = GetAppVersion();
    
    private static string GetAppVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "";
    }
    
    /// <summary>
    /// Event fired when tray icon setting changes (for App.axaml.cs)
    /// </summary>
    public event EventHandler<bool>? TrayIconEnabledChanged;
    
    public MainWindowViewModel(
        RecordingViewModel recording,
        PlaybackViewModel playback,
        FilesViewModel files,
        TextExpansionViewModel textExpansion,
        ScheduleViewModel schedule,
        ShortcutViewModel shortcuts,
        SettingsViewModel settings,
        IGlobalHotkeyService hotkeyService,
        IMousePositionProvider positionProvider)
    {
        Recording = recording;
        Playback = playback;
        Files = files;
        TextExpansion = textExpansion;
        Schedule = schedule;
        Shortcuts = shortcuts;
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

        // Initialize Navigation
        TopNavigationItems = new ObservableCollection<NavigationItem>
        {
            new NavigationItem { Label = "Recording", Icon = "🔴", ViewModel = Recording },
            new NavigationItem { Label = "Playback", Icon = "▶️", ViewModel = Playback },
            new NavigationItem { Label = "Files", Icon = "💾", ViewModel = Files },
            new NavigationItem { Label = "Text Expansion", Icon = "📝", ViewModel = TextExpansion },
            new NavigationItem { Label = "Shortcuts", Icon = "⌨️", ViewModel = Shortcuts },
            new NavigationItem { Label = "Schedule", Icon = "🕐", ViewModel = Schedule }
        };

        BottomNavigationItems = new ObservableCollection<NavigationItem>
        {
            new NavigationItem { Label = "Settings", Icon = "⚙️", ViewModel = Settings }
        };

        SelectedTopItem = TopNavigationItems.First();

        // Check for updates
        _ = CheckForUpdatesAsync();

    }

    // Update Notification Properties
    private bool _isUpdateNotificationVisible;
    private string _latestVersion = string.Empty;
    private string _updateReleaseUrl = string.Empty;

    public bool IsUpdateNotificationVisible
    {
        get => _isUpdateNotificationVisible;
        set
        {
            if (_isUpdateNotificationVisible != value)
            {
                _isUpdateNotificationVisible = value;
                OnPropertyChanged();
            }
        }
    }

    public string LatestVersion
    {
        get => _latestVersion;
        set
        {
            if (_latestVersion != value)
            {
                _latestVersion = value;
                OnPropertyChanged();
            }
        }
    }

    private async System.Threading.Tasks.Task CheckForUpdatesAsync()
    {
        try
        {
            // Check if updates are enabled in settings
            if (!Settings.CheckForUpdates) return;

            var updateService = (Avalonia.Application.Current as App)?.Services?.GetService(typeof(IUpdateService)) as IUpdateService;
            if (updateService == null) return;

            var result = await updateService.CheckForUpdatesAsync();
            if (result.HasUpdate)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    LatestVersion = result.LatestVersion;
                    _updateReleaseUrl = result.ReleaseUrl;
                    IsUpdateNotificationVisible = true;
                });
            }
        }
        catch (Exception ex)
        {
            // Log error but don't disturb user
            System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
        }
    }

    public void DismissUpdateNotification()
    {
        IsUpdateNotificationVisible = false;
    }

    public void OpenUpdateUrl()
    {
        try
        {
            if (!string.IsNullOrEmpty(_updateReleaseUrl))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _updateReleaseUrl,
                    UseShellExecute = true
                });
            }
        }
        catch { }
        finally
        {
            IsUpdateNotificationVisible = false;
        }
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
        
        // When a macro is loaded, update Playback and Recording
        Files.MacroLoaded += (s, macro) =>
        {
            Playback.SetMacro(macro);
            Recording.SetMacro(macro);
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
        Schedule.StatusChanged += (s, status) => GlobalStatus = status;
    }
    
    private void SetupExtensionStatusHandling()
    {
        if (_positionProvider is GnomePositionProvider gnomeProvider)
        {
            gnomeProvider.ExtensionStatusChanged += OnExtensionStatusChanged;
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



    private void UpdateCombinedWarning()
    {
        if (!string.IsNullOrEmpty(_gnomeWarning))
        {
            ExtensionWarning = _gnomeWarning;
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
