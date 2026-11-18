using CrossMacro.Core.Services.Updates;
using CrossMacro.UI.Icons;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// Coordinator ViewModel - manages child ViewModels and cross-cutting concerns
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IExternalUrlOpener _externalUrlOpener;
    private readonly ILocalizationService _localizationService;
    private readonly MainWindowNavigationCatalog _navigationCatalog;
    private readonly IProfileManager? _profileManager;
    private readonly IExtensionStatusNotifier? _extensionNotifier;
    private readonly IUpdateService? _updateService;
    private readonly IEnumerable<IPlatformStartupNotificationProvider> _platformStartupNotificationProviders;
    private readonly DisplayEnvironment _currentEnvironment;
    private bool _disposed;
    private readonly PresentationSubscriptions _subscriptions = new();
    private readonly MainWindowViewModelLinks _links;
    private CancellationTokenSource? _appNotificationCts;

    [ObservableProperty]
    private string _globalStatus;

    internal Task StartupInitializationTask { get; private set; } = Task.CompletedTask;
    private readonly Lock _startupGate = new();
    private bool _startupInitialized;
    private Task? _optionalBackgroundTask;

    public RecordingViewModel Recording { get; }
    public PlaybackViewModel Playback { get; }
    public FilesViewModel Files { get; }
    public TextExpansionViewModel TextExpansion { get; }
    public ScheduleViewModel Schedule { get; }
    public ShortcutViewModel Shortcuts { get; }
    public TriggerViewModel Triggers { get; }
    public SettingsViewModel Settings { get; }
    public EditorWorkspaceViewModel Editor { get; }


    public bool IsCloseButtonVisible { get; }

    [ObservableProperty]
    private bool _isPaneOpen;

    [ObservableProperty]
    private NavigationItem? _selectedTopItem;

    [ObservableProperty]
    private NavigationItem? _selectedBottomItem;

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    public NavigationItem? SelectedNavigationItem
    {
        get;
        private set
        {
            if (SetProperty(ref field, value) && value is not null)
            {
                CurrentPage = value.ViewModel;
            }
        }
    }

    partial void OnSelectedTopItemChanged(NavigationItem? value)
    {
        if (value is not null)
        {
            SelectedBottomItem = null;
            SelectedNavigationItem = value;
        }
    }

    partial void OnSelectedBottomItemChanged(NavigationItem? value)
    {
        if (value is not null)
        {
            SelectedTopItem = null;
            SelectedNavigationItem = value;
        }
    }

    public ObservableCollection<NavigationItem> TopNavigationItems { get; private set; }
    public ObservableCollection<NavigationItem> BottomNavigationItems { get; private set; }


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
        TriggerViewModel triggers,
        SettingsViewModel settings,
        EditorWorkspaceViewModel editor,
        IGlobalHotkeyService hotkeyService,
        IEnvironmentInfoProvider environmentInfo,
        IExternalUrlOpener externalUrlOpener,
        ILocalizationService localizationService,
        IExtensionStatusNotifier? extensionNotifier = null,
        IUpdateService? updateService = null,
        IEnumerable<IPlatformStartupNotificationProvider>? platformStartupNotificationProviders = null,
        IProfileManager? profileManager = null,
        IUiDispatcher? uiDispatcher = null)
        : base(uiDispatcher)
    {
        Recording = recording;
        Playback = playback;
        Files = files;
        TextExpansion = textExpansion;
        Schedule = schedule;
        Shortcuts = shortcuts;
        Triggers = triggers;
        Settings = settings;
        ArgumentNullException.ThrowIfNull(editor);
        Editor = editor;
        _hotkeyService = hotkeyService;
        ArgumentNullException.ThrowIfNull(environmentInfo);
        _externalUrlOpener = externalUrlOpener;
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _navigationCatalog = new MainWindowNavigationCatalog(_localizationService);
        _profileManager = profileManager;
        _extensionNotifier = extensionNotifier;
        _updateService = updateService;
        _platformStartupNotificationProviders = platformStartupNotificationProviders ?? [];
        _currentEnvironment = environmentInfo.CurrentEnvironment;
        _globalStatus = _localizationService["Status_Ready"];
        _localizationService.CultureChanged += OnCultureChanged;

        IsCloseButtonVisible = !environmentInfo.WindowManagerHandlesCloseButton;

        // Wire up cross-ViewModel communication
        _links = new MainWindowViewModelLinks(Recording, Playback, Files, TextExpansion, Schedule, Shortcuts, Triggers, Editor, _localizationService, SetGlobalStatusThreadSafe);

        // Subscribe to hotkey events
        _hotkeyService.ToggleRecordingRequested += OnToggleRecordingRequested;
        _hotkeyService.TogglePlaybackRequested += OnTogglePlaybackRequested;
        _hotkeyService.TogglePauseRequested += OnTogglePauseRequested;

        // Subscribe to extension status events
        SetupExtensionStatusHandling();

        // Subscribe to global hotkey errors
        _hotkeyService.ErrorOccurred += OnGlobalHotkeyError;
        _profileManager?.ProfileChanged += OnProfileChanged;

        // Check for existing errors (in case service started before we subscribed)
        if (!string.IsNullOrEmpty(_hotkeyService.LastError))
        {
            OnGlobalHotkeyError(this, new GlobalHotkeyErrorEventArgs(_hotkeyService.LastError));
        }

        // Forward tray icon changes
        void onSettingsTrayIconEnabledChanged(object? s, bool enabled) => TrayIconEnabledChanged?.Invoke(this, enabled);
        Settings.TrayIconEnabledChanged += onSettingsTrayIconEnabledChanged;
        _subscriptions.Add(() => Settings.TrayIconEnabledChanged -= onSettingsTrayIconEnabledChanged);

        // Initialize Navigation
        TopNavigationItems = _navigationCatalog.CreateTopItems(
            Recording,
            Playback,
            Files,
            TextExpansion,
            Shortcuts,
            Schedule,
            Triggers,
            Editor);
        BottomNavigationItems = _navigationCatalog.CreateBottomItems(Settings);

        SelectedTopItem = TopNavigationItems[0];

    }

    internal Task InitializeAsync()
    {
        lock (_startupGate)
        {
            if (!_startupInitialized)
            {
                _startupInitialized = true;
                StartupInitializationTask = InitializeBackgroundServicesAsync();
            }
            return StartupInitializationTask;
        }
    }

    private async System.Threading.Tasks.Task InitializeBackgroundServicesAsync()
    {
        await Task.WhenAll(Schedule.InitializeAsync(), Shortcuts.InitializeAsync(), Triggers.InitializeAsync(), TextExpansion.InitializeAsync()).ConfigureAwait(false);
        ShowPlatformStartupNotificationIfNeeded();
    }

    internal Task StartOptionalBackgroundWorkAsync(CancellationToken cancellationToken)
    {
        lock (_startupGate)
        {
            return _optionalBackgroundTask ??= CheckForUpdatesAsync(cancellationToken);
        }
    }

    private void ShowPlatformStartupNotificationIfNeeded()
    {
        if (!TryGetPlatformStartupNotification(out var notification))
        {
            return;
        }

        void ShowNotification()
        {
            if (IsAppNotificationVisible)
            {
                return;
            }

            ShowAppNotification(
                title: notification.Title,
                message: notification.Message,
                severity: ToAppNotificationSeverity(notification.Severity),
                duration: TimeSpan.FromSeconds(12));
        }

        UiDispatcher.Post(ShowNotification);
    }

    private bool TryGetPlatformStartupNotification([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PlatformStartupNotification? notification)
    {
        notification = null;
        return !IsAppNotificationVisible
            && PlatformStartupNotificationSelector.TryGet(_platformStartupNotificationProviders, out notification);
    }

    private static AppNotificationSeverity ToAppNotificationSeverity(PlatformStartupNotificationSeverity severity)
    {
        return severity switch
        {
            PlatformStartupNotificationSeverity.Success => AppNotificationSeverity.Success,
            PlatformStartupNotificationSeverity.Error => AppNotificationSeverity.Error,
            PlatformStartupNotificationSeverity.Warning => AppNotificationSeverity.Warning,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, message: null),
        };
    }

    // Update Notification Properties
    private string _updateReleaseUrl = string.Empty;

    [ObservableProperty]
    private bool _isUpdateNotificationVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateAvailableVersionText))]
    private string _latestVersion = string.Empty;

    public string UpdateAvailableVersionText => string.Format(
        _localizationService.CurrentCulture,
        _localizationService["MainWindow_UpdateAvailableVersion"],
        LatestVersion);

    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Check if updates are enabled in settings
            if (!Settings.CheckForUpdates)
            {
                return;
            }

            if (_updateService is null)
            {
                return;
            }

            var result = await _updateService.CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
            if (result.HasUpdate)
            {
                UiDispatcher.Post(() =>
                {
                    if (_disposed || cancellationToken.IsCancellationRequested) { return; }
                    LatestVersion = result.LatestVersion;
                    _updateReleaseUrl = result.ReleaseUrl?.ToString() ?? string.Empty;
                    IsUpdateNotificationVisible = true;
                });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The desktop lifetime owns cancellation of optional work.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Log error but don't disturb user
            Log.Warning(ex, "Update check failed");
        }
    }

    [RelayCommand]
    public void DismissUpdateNotification()
    {
        IsUpdateNotificationVisible = false;
    }

    [RelayCommand]
    public void OpenUpdateUrl()
    {
        var url = _updateReleaseUrl;
        IsUpdateNotificationVisible = false;
        if (!string.IsNullOrEmpty(url))
        {
            _ = OpenUpdateUrlAsync(url);
        }
    }

    private async Task OpenUpdateUrlAsync(string url)
    {
        try
        {
            await _externalUrlOpener.OpenAsync(new Uri(url, UriKind.Absolute)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[MainWindowViewModel] Failed to open the update URL");
        }
    }

    private void OnProfileChanged(object? sender, ProfileChangedEventArgs e)
    {
        // The trigger tab subscribes to profile changes itself.
        UiDispatcher.Post(() =>
        {
            Playback.RefreshProfileSettings();
            Recording.RefreshProfileSettings();
            _ = TextExpansion.RefreshProfileDataAsync();
            Schedule.RefreshProfileData();
            Shortcuts.RefreshProfileData();
        });
    }

    private void SetGlobalStatusThreadSafe(string status) => UiDispatcher.Post(() => GlobalStatus = status);

    private void SetupExtensionStatusHandling()
    {
        // Subscribe via Core interface - no platform-specific type checking needed
        if (_extensionNotifier is not null)
        {
            _extensionNotifier.ExtensionStatusUpdated += OnExtensionStatusUpdated;
            if (_extensionNotifier.CurrentExtensionStatus is { } currentStatus)
            {
                OnExtensionStatusUpdated(_extensionNotifier, currentStatus);
            }
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        _navigationCatalog.RefreshLabels(TopNavigationItems, BottomNavigationItems);

        RefreshIdleGlobalStatus();

        OnPropertyChanged(nameof(UpdateAvailableVersionText));
    }

    private void RefreshIdleGlobalStatus()
    {
        if (Recording.IsRecording || Playback.IsPlaying)
        {
            return;
        }

        if (Files.CurrentMacro is not null)
        {
            SetGlobalStatusThreadSafe(Recording.RecordingStatus);
            return;
        }

        SetGlobalStatusThreadSafe(_localizationService["Status_Ready"]);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExtensionWarning))]
    private string? _extensionWarning;

    public bool HasExtensionWarning => !string.IsNullOrEmpty(ExtensionWarning);

    [ObservableProperty]
    private bool _isAppNotificationVisible;

    [ObservableProperty]
    private string _appNotificationTitle = string.Empty;

    [ObservableProperty]
    private string _appNotificationMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppNotificationIcon))]
    [NotifyPropertyChangedFor(nameof(IsAppNotificationSuccess))]
    [NotifyPropertyChangedFor(nameof(IsAppNotificationError))]
    [NotifyPropertyChangedFor(nameof(IsAppNotificationWarning))]
    private AppNotificationSeverity _notificationSeverity;

    public AppIcon AppNotificationIcon => NotificationSeverity is AppNotificationSeverity.Success ? AppIcon.Success : AppIcon.Warning;

    public bool IsAppNotificationSuccess => NotificationSeverity is AppNotificationSeverity.Success;

    public bool IsAppNotificationError => NotificationSeverity is AppNotificationSeverity.Error;

    public bool IsAppNotificationWarning => NotificationSeverity is AppNotificationSeverity.Warning;

    private void OnExtensionStatusUpdated(object? sender, ExtensionStatusChangedEventArgs e)
    {
        UiDispatcher.Post(() =>
        {
            if (e.Code is ExtensionStatusCode.Enabled)
            {
                ExtensionWarning = null;
                ShowAppNotification(
                    title: _localizationService["MainWindow_GnomeExtensionTitle"],
                    message: e.Message,
                    severity: AppNotificationSeverity.Success,
                    duration: TimeSpan.FromSeconds(3));
                return;
            }

            ExtensionWarning = string.IsNullOrEmpty(e.Message) ? null : e.Message;
            ShowAppNotification(
                title: _localizationService["MainWindow_GnomeExtensionTitle"],
                message: e.Message,
                severity: e.Code is ExtensionStatusCode.Error
                    ? AppNotificationSeverity.Error
                    : AppNotificationSeverity.Warning,
                duration: TimeSpan.FromSeconds(10));
        });
    }

    private void OnToggleRecordingRequested(object? sender, EventArgs e)
    {
        UiDispatcher.Post(() =>
        {
            Recording.ToggleRecording();
        });
    }

    private void OnTogglePlaybackRequested(object? sender, EventArgs e)
    {
        UiDispatcher.Post(() =>
        {
            Playback.TogglePlayback();
        });
    }

    private void OnTogglePauseRequested(object? sender, EventArgs e)
    {
        UiDispatcher.Post(() =>
        {
            Playback.TogglePause();
        });
    }

    private void OnGlobalHotkeyError(object? sender, GlobalHotkeyErrorEventArgs e)
    {
        var error = e.Message;
        UiDispatcher.Post(() =>
        {
            var troubleshootingHintKey = MainWindowPresentationPolicy.GetBackendTroubleshootingHintKey(_currentEnvironment);
            var message = troubleshootingHintKey is null
                ? error
                : $"{error}\n\n{string.Format(
                    _localizationService.CurrentCulture,
                    _localizationService["MainWindow_BackendTroubleshootingFormat"],
                    _localizationService[troubleshootingHintKey])}";

            ShowAppNotification(
                title: _localizationService["MainWindow_BackendErrorTitle"],
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

    private void ShowAppNotification(string title, string message, AppNotificationSeverity severity, TimeSpan duration)
    {
        CancelAppNotificationTimer();
        var notificationCts = new CancellationTokenSource();
        _appNotificationCts = notificationCts;
        var token = notificationCts.Token;

        AppNotificationTitle = title;
        AppNotificationMessage = message;
        NotificationSeverity = severity;
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
            await Task.Delay(duration, TimeProvider.System, token).ConfigureAwait(false);

            if (token.IsCancellationRequested || !ReferenceEquals(_appNotificationCts, notificationCts))
            {
                return;
            }

            UiDispatcher.Post(ResetAppNotificationState);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Expected when the notification is dismissed or replaced.
        }
        finally
        {
            if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _appNotificationCts, value: null, notificationCts),
                    notificationCts))
            {
                notificationCts.Dispose();
            }
        }
    }

    private void CancelAppNotificationTimer()
    {
        var notificationCts = Interlocked.Exchange(ref _appNotificationCts, value: null);
        if (notificationCts is null)
        {
            return;
        }

        notificationCts.Cancel();
        notificationCts.Dispose();
    }

    private void ResetAppNotificationState()
    {
        IsAppNotificationVisible = false;
        AppNotificationTitle = string.Empty;
        AppNotificationMessage = string.Empty;
        NotificationSeverity = AppNotificationSeverity.None;
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

        CancelAppNotificationTimer();
        _links.Dispose();
        _subscriptions.Dispose();
        _localizationService.CultureChanged -= OnCultureChanged;

        // Unsubscribe from hotkey events
        _hotkeyService.ToggleRecordingRequested -= OnToggleRecordingRequested;
        _hotkeyService.TogglePlaybackRequested -= OnTogglePlaybackRequested;
        _hotkeyService.TogglePauseRequested -= OnTogglePauseRequested;
        _hotkeyService.ErrorOccurred -= OnGlobalHotkeyError;
        _profileManager?.ProfileChanged -= OnProfileChanged;

        // Unsubscribe from extension status events
        _extensionNotifier?.ExtensionStatusUpdated -= OnExtensionStatusUpdated;

        // The DI scope owns child models, including the editor workspace.
    }

    public enum AppNotificationSeverity
    {
        None,
        Success,
        Warning,
        Error,
    }
}
