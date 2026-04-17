using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Models;
using CrossMacro.UI.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// Coordinator ViewModel - manages child ViewModels and cross-cutting concerns
/// </summary>
public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IMousePositionProvider _positionProvider;
    private readonly IExternalUrlOpener _externalUrlOpener;
    private readonly IExtensionStatusNotifier? _extensionNotifier;
    private readonly IUpdateService? _updateService;
    private readonly DisplayEnvironment _currentEnvironment;
    
    private string? _extensionWarning;
    private bool _hasExtensionWarning;
    
    private string? _gnomeWarning;
    private bool _disposed;
    private CancellationTokenSource? _appNotificationCts;

    private string _globalStatus = "Ready";
    private bool _isAppNotificationVisible;
    private string _appNotificationTitle = string.Empty;
    private string _appNotificationMessage = string.Empty;
    private string _appNotificationIcon = "⚠️";
    private bool _isAppNotificationSuccess;
    private bool _isAppNotificationError;
    private bool _isAppNotificationWarning;
    private bool _suppressRecordingStatusForwarding;
    private bool _suppressSelectedMacroRecordingSync;
    
    public RecordingViewModel Recording { get; }
    public PlaybackViewModel Playback { get; }
    public FilesViewModel Files { get; }
    public TextExpansionViewModel TextExpansion { get; }
    public ScheduleViewModel Schedule { get; }
    public ShortcutViewModel Shortcuts { get; }
    public SettingsViewModel Settings { get; }
    public EditorViewModel Editor { get; }
    
    
    public bool IsCloseButtonVisible { get; }

    private bool _isPaneOpen = false;
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
        EditorViewModel editor,
        IGlobalHotkeyService hotkeyService,
        IMousePositionProvider positionProvider,
        IEnvironmentInfoProvider environmentInfo,
        IExternalUrlOpener externalUrlOpener,
        IExtensionStatusNotifier? extensionNotifier = null,
        IUpdateService? updateService = null)
    {
        Recording = recording;
        Playback = playback;
        Files = files;
        TextExpansion = textExpansion;
        Schedule = schedule;
        Shortcuts = shortcuts;
        Settings = settings;
        Editor = editor;
        _hotkeyService = hotkeyService;
        _positionProvider = positionProvider;
        _externalUrlOpener = externalUrlOpener;
        _extensionNotifier = extensionNotifier;
        _updateService = updateService;
        _currentEnvironment = environmentInfo.CurrentEnvironment;
        
        // Use abstraction for close button visibility (DIP: depends on Core interface)
        IsCloseButtonVisible = !environmentInfo.WindowManagerHandlesCloseButton;
        
        // Wire up cross-ViewModel communication
        SetupViewModelCommunication();
        
        // Subscribe to hotkey events
        _hotkeyService.ToggleRecordingRequested += OnToggleRecordingRequested;
        _hotkeyService.TogglePlaybackRequested += OnTogglePlaybackRequested;
        _hotkeyService.TogglePauseRequested += OnTogglePauseRequested;
        
        // Subscribe to extension status events
        SetupExtensionStatusHandling();
        
        // Subscribe to global hotkey errors
        _hotkeyService.ErrorOccurred += OnGlobalHotkeyError;
        
        // Check for existing errors (in case service started before we subscribed)
        if (!string.IsNullOrEmpty(_hotkeyService.LastError))
        {
            OnGlobalHotkeyError(this, _hotkeyService.LastError);
        }

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
            new NavigationItem { Label = "Schedule", Icon = "🕐", ViewModel = Schedule },
            new NavigationItem { Label = "Editor", Icon = "🛠️", ViewModel = Editor }
        };
        


        BottomNavigationItems = new ObservableCollection<NavigationItem>
        {
            new NavigationItem { Label = "Settings", Icon = "⚙️", ViewModel = Settings }
        };

        SelectedTopItem = TopNavigationItems.First();

        _ = InitializeBackgroundServicesAsync();

    }

    private async System.Threading.Tasks.Task InitializeBackgroundServicesAsync()
    {
        await Schedule.InitializeAsync();
        await CheckForUpdatesAsync();
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

            if (_updateService == null) return;

            var result = await _updateService.CheckForUpdatesAsync();
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
                _externalUrlOpener.Open(_updateReleaseUrl);
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
        // When recording completes, add the macro to the session and select it
        Recording.RecordingCompleted += (s, macro) =>
        {
            try
            {
                _suppressSelectedMacroRecordingSync = true;
                Files.SetMacro(macro);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[MainWindowViewModel] Failed to sync recorded macro to FilesViewModel");
            }
            finally
            {
                _suppressSelectedMacroRecordingSync = false;
            }

            var eventCount = macro?.Events?.Count ?? 0;
            SetGlobalStatusThreadSafe($"Recorded {eventCount} events");
        };
        
        // When recording state changes, update Playback's ability to start
        Recording.RecordingStateChanged += (s, isRecording) =>
        {
            Playback.CanPlayMacroExternal = !isRecording;
        };
        
        // When playback state changes, update Recording's ability to start and freeze Files interactions
        Playback.PlaybackStateChanged += (s, isPlaying) =>
        {
            Recording.CanStartRecordingExternal = !isPlaying;
            Files.CanManageLoadedMacrosExternal = !isPlaying;

            if (!isPlaying)
            {
                SyncRecordingMacroSummary();
            }
        };

        void SyncSelectedMacroSummary(object? _, EventArgs __)
        {
            if (_suppressSelectedMacroRecordingSync)
            {
                return;
            }

            SyncRecordingMacroSummary();
        }

        // Keep recording statistics in sync when selection changes or the selected macro payload is replaced.
        Files.SelectedMacroChanged += SyncSelectedMacroSummary;
        Files.SelectedMacroUpdated += SyncSelectedMacroSummary;
        
        // When a macro is loaded from disk, update global status.
        Files.MacroLoaded += (s, macro) =>
        {
            SetGlobalStatusThreadSafe($"Loaded: {macro.Name}");
        };
        
        // When a macro is created in Editor, update the linked loaded macro or add a new one.
        Editor.MacroCreated += (s, e) =>
        {
            var linkedItem = Files.UpsertMacro(Editor.LinkedLoadedMacroSessionId, e.Macro, e.SourcePath);
            if (linkedItem != null)
            {
                Editor.TrackLoadedMacroSession(linkedItem.SessionId);
            }

            SetGlobalStatusThreadSafe($"Created: {e.Macro.Name} ({e.Macro.EventCount} events)");
        };
        
        // Forward status changes
        Recording.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Recording.RecordingStatus) && !_suppressRecordingStatusForwarding)
            {
                SetGlobalStatusThreadSafe(Recording.RecordingStatus);
            }
        };
        
        Playback.StatusChanged += (s, status) => SetGlobalStatusThreadSafe(status);
        Files.StatusChanged += (s, status) => SetGlobalStatusThreadSafe(status);
        Schedule.StatusChanged += (s, status) => SetGlobalStatusThreadSafe(status);
        Editor.StatusChanged += (s, status) => SetGlobalStatusThreadSafe(status);
    }

    private void SyncRecordingMacroSummary()
    {
        if (Playback.IsPlaying || Recording.IsRecording)
        {
            return;
        }

        try
        {
            _suppressRecordingStatusForwarding = true;
            Recording.SetMacro(Files.GetCurrentMacro(), updateStatus: true);
        }
        finally
        {
            _suppressRecordingStatusForwarding = false;
        }
    }

    private void SetGlobalStatusThreadSafe(string status)
    {
        if (Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            GlobalStatus = status;
            return;
        }

        Dispatcher.UIThread.Post(() => GlobalStatus = status);
    }
    
    private void SetupExtensionStatusHandling()
    {
        // Subscribe via Core interface - no platform-specific type checking needed
        if (_extensionNotifier != null)
        {
            _extensionNotifier.ExtensionStatusUpdated += OnExtensionStatusUpdated;
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

    public bool IsAppNotificationVisible
    {
        get => _isAppNotificationVisible;
        set
        {
            if (_isAppNotificationVisible != value)
            {
                _isAppNotificationVisible = value;
                OnPropertyChanged();
            }
        }
    }

    public string AppNotificationTitle
    {
        get => _appNotificationTitle;
        set
        {
            if (_appNotificationTitle != value)
            {
                _appNotificationTitle = value;
                OnPropertyChanged();
            }
        }
    }

    public string AppNotificationMessage
    {
        get => _appNotificationMessage;
        set
        {
            if (_appNotificationMessage != value)
            {
                _appNotificationMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public string AppNotificationIcon
    {
        get => _appNotificationIcon;
        set
        {
            if (_appNotificationIcon != value)
            {
                _appNotificationIcon = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsAppNotificationSuccess
    {
        get => _isAppNotificationSuccess;
        set
        {
            if (_isAppNotificationSuccess != value)
            {
                _isAppNotificationSuccess = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsAppNotificationError
    {
        get => _isAppNotificationError;
        set
        {
            if (_isAppNotificationError != value)
            {
                _isAppNotificationError = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsAppNotificationWarning
    {
        get => _isAppNotificationWarning;
        set
        {
            if (_isAppNotificationWarning != value)
            {
                _isAppNotificationWarning = value;
                OnPropertyChanged();
            }
        }
    }
    
    private void OnExtensionStatusUpdated(object? sender, ExtensionStatusChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (e.Code == ExtensionStatusCode.Enabled)
            {
                ShowAppNotification(
                    title: "GNOME Extension",
                    message: e.Message,
                    severity: AppNotificationSeverity.Success,
                    duration: TimeSpan.FromSeconds(3));
                
                // Clear warning if it was set
                if (_gnomeWarning != null)
                {
                    _gnomeWarning = null;
                    UpdateCombinedWarning();
                }
                return;
            }
            
            _gnomeWarning = e.Message;
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

    private void OnGlobalHotkeyError(object? sender, string error)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var troubleshootingHint = GetBackendTroubleshootingHint(_currentEnvironment);
            var message = string.IsNullOrWhiteSpace(troubleshootingHint)
                ? error
                : $"{error}\n\nTroubleshooting: {troubleshootingHint}";

            ShowAppNotification(
                title: "Backend Error",
                message: message,
                severity: AppNotificationSeverity.Error,
                duration: TimeSpan.FromSeconds(10));
        });
    }

    public void DismissAppNotification()
    {
        CancelAppNotificationTimer();
        ResetAppNotificationState();
    }

    private static string? GetBackendTroubleshootingHint(DisplayEnvironment environment)
    {
        return environment switch
        {
            DisplayEnvironment.LinuxX11
                or DisplayEnvironment.LinuxWayland
                or DisplayEnvironment.LinuxHyprland
                or DisplayEnvironment.LinuxKDE
                or DisplayEnvironment.LinuxGnome
                => "check daemon status with `systemctl status crossmacro`.",
            DisplayEnvironment.Windows
                => "restart CrossMacro and verify the background service is running.",
            DisplayEnvironment.MacOS
                => "restart CrossMacro and verify Accessibility permissions in System Settings.",
            _ => null
        };
    }

    private void ShowAppNotification(string title, string message, AppNotificationSeverity severity, TimeSpan duration)
    {
        CancelAppNotificationTimer();
        var notificationCts = new CancellationTokenSource();
        _appNotificationCts = notificationCts;
        var token = notificationCts.Token;

        AppNotificationTitle = title;
        AppNotificationMessage = message;
        AppNotificationIcon = severity switch
        {
            AppNotificationSeverity.Success => "✅",
            AppNotificationSeverity.Error => "⚠️",
            _ => "⚠️"
        };
        IsAppNotificationSuccess = severity == AppNotificationSeverity.Success;
        IsAppNotificationError = severity == AppNotificationSeverity.Error;
        IsAppNotificationWarning = severity == AppNotificationSeverity.Warning;
        IsAppNotificationVisible = true;

        _ = DismissAppNotificationAfterDelayAsync(notificationCts, duration, token);
    }

    private async Task DismissAppNotificationAfterDelayAsync(
        CancellationTokenSource notificationCts,
        TimeSpan duration,
        CancellationToken token)
    {
        try
        {
            await Task.Delay(duration, token);

            if (token.IsCancellationRequested || !ReferenceEquals(_appNotificationCts, notificationCts))
            {
                return;
            }

            ResetAppNotificationState();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_appNotificationCts, notificationCts))
            {
                notificationCts.Dispose();
                _appNotificationCts = null;
            }
        }
    }

    private void CancelAppNotificationTimer()
    {
        if (_appNotificationCts == null)
        {
            return;
        }

        _appNotificationCts.Cancel();
        _appNotificationCts.Dispose();
        _appNotificationCts = null;
    }

    private void ResetAppNotificationState()
    {
        IsAppNotificationVisible = false;
        AppNotificationTitle = string.Empty;
        AppNotificationMessage = string.Empty;
        AppNotificationIcon = "⚠️";
        IsAppNotificationSuccess = false;
        IsAppNotificationError = false;
        IsAppNotificationWarning = false;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CancelAppNotificationTimer();
        
        // Unsubscribe from hotkey events
        _hotkeyService.ToggleRecordingRequested -= OnToggleRecordingRequested;
        _hotkeyService.TogglePlaybackRequested -= OnTogglePlaybackRequested;
        _hotkeyService.TogglePauseRequested -= OnTogglePauseRequested;
        _hotkeyService.ErrorOccurred -= OnGlobalHotkeyError;
        
        // Unsubscribe from extension status events
        if (_extensionNotifier != null)
        {
            _extensionNotifier.ExtensionStatusUpdated -= OnExtensionStatusUpdated;
        }
        
        // Dispose child ViewModels that implement IDisposable
        Recording.Dispose();
        Schedule.Dispose();
        Shortcuts.Dispose();
    }

    private enum AppNotificationSeverity
    {
        Success,
        Warning,
        Error
    }
}
