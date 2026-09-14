
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
    private string? _gnomeWarning;
    private bool _disposed;
    private readonly PresentationSubscriptions _subscriptions = new();
    private CancellationTokenSource? _appNotificationCts;

    private string _globalStatus;
    private bool _suppressRecordingStatusForwarding;
    private bool _suppressSelectedMacroRecordingSync;

    internal Task StartupInitializationTask { get; private set; } = Task.CompletedTask;
    private readonly Lock _startupGate = new();
    private bool _startupInitialized;

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

    public bool IsPaneOpen
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public NavigationItem? SelectedTopItem
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();

                if (value is not null)
                {
                    SelectedBottomItem = null;
                    SelectedNavigationItem = value;
                }
            }
        }
    }

    public NavigationItem? SelectedBottomItem
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();

                if (value is not null)
                {
                    SelectedTopItem = null;
                    SelectedNavigationItem = value;
                }
            }
        }
    }

    public NavigationItem? SelectedNavigationItem
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                if (value is not null)
                {
                    CurrentPage = value.ViewModel;
                }
            }
        }
    }

    public ViewModelBase? CurrentPage
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
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
        return Program.GetDisplayVersionString();
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
        TriggerViewModel triggers,
        SettingsViewModel settings,
        EditorWorkspaceViewModel editor,
        IGlobalHotkeyService hotkeyService,
        IMousePositionProvider positionProvider,
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
        ArgumentNullException.ThrowIfNull(positionProvider);
        ArgumentNullException.ThrowIfNull(environmentInfo);
        _externalUrlOpener = externalUrlOpener;
        _localizationService = localizationService ?? new LocalizationService();
        _navigationCatalog = new MainWindowNavigationCatalog(_localizationService);
        _profileManager = profileManager;
        _extensionNotifier = extensionNotifier;
        _updateService = updateService;
        _platformStartupNotificationProviders = platformStartupNotificationProviders ?? [];
        _currentEnvironment = environmentInfo.CurrentEnvironment;
        _globalStatus = _localizationService["Status_Ready"];
        _localizationService.CultureChanged += OnCultureChanged;

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
        await CheckForUpdatesAsync().ConfigureAwait(false);
        ShowPlatformStartupNotificationIfNeeded();
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

        if (UiDispatcher.CheckAccess())
        {
            ShowNotification();
            return;
        }

        UiDispatcher.Post(ShowNotification);
    }

    private bool TryGetPlatformStartupNotification([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PlatformStartupNotification? notification)
    {
        notification = null;

        if (IsAppNotificationVisible)
        {
            return false;
        }

        foreach (var provider in _platformStartupNotificationProviders)
        {
            try
            {
                notification = provider.GetStartupNotification();
                if (notification != null)
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Warning(ex, "[MainWindowViewModel] Platform startup notification provider failed");
            }
        }

        return false;
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

    public bool IsUpdateNotificationVisible
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public string LatestVersion
    {
        get;
        set
        {
            if (!string.Equals(field, value, StringComparison.Ordinal))
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = string.Empty;

    public string UpdateAvailableVersionText => string.Format(
        _localizationService.CurrentCulture,
        _localizationService["MainWindow_UpdateAvailableVersion"],
        LatestVersion);

    private async System.Threading.Tasks.Task CheckForUpdatesAsync()
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

            var result = await _updateService.CheckForUpdatesAsync().ConfigureAwait(false);
            if (result.HasUpdate)
            {
                void ApplyUpdateNotification()
                {
                    LatestVersion = result.LatestVersion;
                    _updateReleaseUrl = result.ReleaseUrl?.ToString() ?? string.Empty;
                    IsUpdateNotificationVisible = true;
                    OnPropertyChanged(nameof(UpdateAvailableVersionText));
                }

                if (UiDispatcher.CheckAccess())
                {
                    ApplyUpdateNotification();
                }
                else
                {
                    UiDispatcher.Post(ApplyUpdateNotification);
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Log error but don't disturb user
            System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
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
        try
        {
            if (!string.IsNullOrEmpty(_updateReleaseUrl))
            {
                ObserveTask(OpenUpdateUrlAsync(_updateReleaseUrl));
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { /* Empty */ }
        finally
        {
            IsUpdateNotificationVisible = false;
        }
    }

    private async Task OpenUpdateUrlAsync(string url)
    {
        try
        {
            await _externalUrlOpener.OpenAsync(new Uri(url, UriKind.Absolute)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { /* Empty */ }
    }

    private static void ObserveTask(Task task)
    {
        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void SetupViewModelCommunication()
    {
        // When recording completes, add the macro to the session and select it
        void onRecordingRecordingCompleted(object? s, MacroSequence macro)
        {
            try
            {
                _suppressSelectedMacroRecordingSync = true;
                Files.SetMacro(macro);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[MainWindowViewModel] Failed to sync recorded macro to FilesViewModel");
            }
            finally
            {
                _suppressSelectedMacroRecordingSync = false;
            }

            var eventCount = macro?.Events?.Count ?? 0;
            SetGlobalStatusThreadSafe(string.Format(
                _localizationService.CurrentCulture,
                _localizationService["Status_RecordedEvents"],
                eventCount));
        }
        Recording.RecordingCompleted += onRecordingRecordingCompleted;
        _subscriptions.Add(() => Recording.RecordingCompleted -= onRecordingRecordingCompleted);

        // When recording state changes, update Playback's ability to start
        void onRecordingRecordingStateChanged(object? s, bool isRecording)
        {
            Playback.CanPlayMacroExternal = !isRecording;
        }
        Recording.RecordingStateChanged += onRecordingRecordingStateChanged;
        _subscriptions.Add(() => Recording.RecordingStateChanged -= onRecordingRecordingStateChanged);

        // When playback state changes, update Recording's ability to start and freeze Files interactions
        void onPlaybackPlaybackStateChanged(object? s, bool isPlaying)
        {
            Recording.CanStartRecordingExternal = !isPlaying;
            Files.CanManageLoadedMacrosExternal = !isPlaying;

            if (!isPlaying)
            {
                SyncRecordingMacroSummary();
            }
        }
        Playback.PlaybackStateChanged += onPlaybackPlaybackStateChanged;
        _subscriptions.Add(() => Playback.PlaybackStateChanged -= onPlaybackPlaybackStateChanged);

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
        _subscriptions.Add(() => Files.SelectedMacroChanged -= SyncSelectedMacroSummary);
        Files.SelectedMacroUpdated += SyncSelectedMacroSummary;
        _subscriptions.Add(() => Files.SelectedMacroUpdated -= SyncSelectedMacroSummary);

        // When a macro is loaded from disk, update global status.
        void onFilesMacroLoaded(object? s, MacroSequence macro)
        {
            SetGlobalStatusThreadSafe(string.Format(
                _localizationService.CurrentCulture,
                _localizationService["Status_LoadedMacro"],
                macro.Name));
        }
        Files.MacroLoaded += onFilesMacroLoaded;
        _subscriptions.Add(() => Files.MacroLoaded -= onFilesMacroLoaded);

        // When a macro is created in Editor, update the linked loaded macro or add a new one.
        void onEditorMacroCreated(object? s, EditorDocumentMacroCreatedEventArgs e)
        {
            LoadedMacroListItem? linkedItem = null;
            if (e.Document.LinkedLoadedMacroSessionId is { } sessionId)
            {
                linkedItem = Files.UpsertMacro(sessionId, e.MacroCreated.Macro, e.MacroCreated.SourcePath, addIfMissing: false);
                if (linkedItem is null)
                {
                    e.Document.ClearLoadedMacroSessionLink();
                }
            }
            else if (e.Document.ShouldAddToPlaybackOnSave)
            {
                linkedItem = Files.UpsertMacro(sessionId: null, e.MacroCreated.Macro, e.MacroCreated.SourcePath);
            }

            if (linkedItem is not null)
            {
                e.Document.TrackLoadedMacroSession(linkedItem.SessionId);
            }

            SetGlobalStatusThreadSafe(string.Format(
                _localizationService.CurrentCulture,
                _localizationService["Status_CreatedMacro"],
                e.MacroCreated.Macro.Name,
                MacroPlayableActionCounter.CountPlayableActions(e.MacroCreated.Macro)));
        }
        Editor.MacroCreated += onEditorMacroCreated;
        _subscriptions.Add(() => Editor.MacroCreated -= onEditorMacroCreated);

        void onEditorPlaybackAddRequested(object? s, EditorDocumentPlaybackAddRequestedEventArgs e)
        {
            var item = Files.UpsertMacro(sessionId: null, e.PlaybackRequested.Macro, e.PlaybackRequested.SourcePath);
            if (item is not null)
            {
                e.Document.TrackLoadedMacroSession(item.SessionId);
            }
        }
        Editor.PlaybackAddRequested += onEditorPlaybackAddRequested;
        _subscriptions.Add(() => Editor.PlaybackAddRequested -= onEditorPlaybackAddRequested);

        Files.LoadedMacroRemoved += OnLoadedMacroRemoved;

        // Forward status changes
        void onRecordingPropertyChanged(object? s, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(Recording.RecordingStatus), StringComparison.Ordinal) && !_suppressRecordingStatusForwarding)
            {
                SetGlobalStatusThreadSafe(Recording.RecordingStatus);
            }
        }
        Recording.PropertyChanged += onRecordingPropertyChanged;
        _subscriptions.Add(() => Recording.PropertyChanged -= onRecordingPropertyChanged);

        void onPlaybackStatusChanged(object? s, string status) => SetGlobalStatusThreadSafe(status);
        Playback.StatusChanged += onPlaybackStatusChanged;
        _subscriptions.Add(() => Playback.StatusChanged -= onPlaybackStatusChanged);
        void onFilesStatusChanged(object? s, string status) => SetGlobalStatusThreadSafe(status);
        Files.StatusChanged += onFilesStatusChanged;
        _subscriptions.Add(() => Files.StatusChanged -= onFilesStatusChanged);
        void onScheduleStatusChanged(object? s, string status) => SetGlobalStatusThreadSafe(status);
        Schedule.StatusChanged += onScheduleStatusChanged;
        _subscriptions.Add(() => Schedule.StatusChanged -= onScheduleStatusChanged);
        void onShortcutsStatusChanged(object? s, string status) => SetGlobalStatusThreadSafe(status);
        Shortcuts.StatusChanged += onShortcutsStatusChanged;
        _subscriptions.Add(() => Shortcuts.StatusChanged -= onShortcutsStatusChanged);
        void onTriggersStatusChanged(object? s, string status) => SetGlobalStatusThreadSafe(status);
        Triggers.StatusChanged += onTriggersStatusChanged;
        _subscriptions.Add(() => Triggers.StatusChanged -= onTriggersStatusChanged);
        void onEditorStatusChanged(object? s, string status) => SetGlobalStatusThreadSafe(status);
        Editor.StatusChanged += onEditorStatusChanged;
        _subscriptions.Add(() => Editor.StatusChanged -= onEditorStatusChanged);
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
            Recording.SetMacro(Files.CurrentMacro, updateStatus: true);
        }
        finally
        {
            _suppressRecordingStatusForwarding = false;
        }
    }

    private void OnLoadedMacroRemoved(object? sender, Guid sessionId)
    {
        Editor.ClearLoadedMacroSessionLink(sessionId);
    }

    private void OnProfileChanged(object? sender, ProfileChangedEventArgs e)
    {
        void RefreshProfileBackedViewModels()
        {
            Playback.RefreshProfileSettings();
            Recording.RefreshProfileSettings();
            _ = TextExpansion.RefreshProfileDataAsync();
            Schedule.RefreshProfileData();
            Shortcuts.RefreshProfileData();
            Triggers.RefreshProfileData();
        }

        if (UiDispatcher.CheckAccess())
        {
            RefreshProfileBackedViewModels();
            return;
        }

        UiDispatcher.Post(RefreshProfileBackedViewModels);
    }

    private void SetGlobalStatusThreadSafe(string status)
    {
        if (UiDispatcher.CheckAccess())
        {
            GlobalStatus = status;
            return;
        }

        UiDispatcher.Post(() => GlobalStatus = status);
    }

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

    public string? ExtensionWarning
    {
        get;
        set
        {
            if (!string.Equals(field, value, StringComparison.Ordinal))
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasExtensionWarning
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public string GlobalStatus
    {
        get => _globalStatus;
        set
        {
            if (!string.Equals(_globalStatus, value, StringComparison.Ordinal))
            {
                _globalStatus = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsAppNotificationVisible
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public string AppNotificationTitle
    {
        get;
        set
        {
            if (!string.Equals(field, value, StringComparison.Ordinal))
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = string.Empty;

    public string AppNotificationMessage
    {
        get;
        set
        {
            if (!string.Equals(field, value, StringComparison.Ordinal))
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = string.Empty;

    public AppIcon AppNotificationIcon
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = AppIcon.Warning;

    public bool IsAppNotificationSuccess
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsAppNotificationError
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsAppNotificationWarning
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    private void OnExtensionStatusUpdated(object? sender, ExtensionStatusChangedEventArgs e)
    {
        void ApplyStatusUpdate()
        {
            if (e.Code is ExtensionStatusCode.Enabled)
            {
                ShowAppNotification(
                    title: _localizationService["MainWindow_GnomeExtensionTitle"],
                    message: e.Message,
                    severity: AppNotificationSeverity.Success,
                    duration: TimeSpan.FromSeconds(3));

                // Clear warning if it was set
                if (_gnomeWarning is not null)
                {
                    _gnomeWarning = null;
                    UpdateCombinedWarning();
                }
                return;
            }

            _gnomeWarning = e.Message;
            UpdateCombinedWarning();
            ShowAppNotification(
                title: _localizationService["MainWindow_GnomeExtensionTitle"],
                message: e.Message,
                severity: e.Code is ExtensionStatusCode.Error
                    ? AppNotificationSeverity.Error
                    : AppNotificationSeverity.Warning,
                duration: TimeSpan.FromSeconds(10));
        }

        if (UiDispatcher.CheckAccess())
        {
            ApplyStatusUpdate();
            return;
        }

        UiDispatcher.Post(ApplyStatusUpdate);
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
            var troubleshootingHintKey = GetBackendTroubleshootingHintKey(_currentEnvironment);
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

    internal static string? GetBackendTroubleshootingHintKey(DisplayEnvironment environment)
    {
        return MainWindowPresentationPolicy.GetBackendTroubleshootingHintKey(environment);
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
            AppNotificationSeverity.Success => AppIcon.Success,
            AppNotificationSeverity.Error => AppIcon.Warning,
            AppNotificationSeverity.Warning => AppIcon.Warning,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, message: null),
        };
        IsAppNotificationSuccess = severity is AppNotificationSeverity.Success;
        IsAppNotificationError = severity is AppNotificationSeverity.Error;
        IsAppNotificationWarning = severity is AppNotificationSeverity.Warning;
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

            PostToUiThreadIfNeeded(ResetAppNotificationState);
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

    private void PostToUiThreadIfNeeded(Action action)
    {
        if (UiDispatcher.CheckAccess())
        {
            action();
            return;
        }

        UiDispatcher.Post(action);
    }

    private void ResetAppNotificationState()
    {
        IsAppNotificationVisible = false;
        AppNotificationTitle = string.Empty;
        AppNotificationMessage = string.Empty;
        AppNotificationIcon = AppIcon.Warning;
        IsAppNotificationSuccess = false;
        IsAppNotificationError = false;
        IsAppNotificationWarning = false;
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
        _subscriptions.Dispose();
        _localizationService.CultureChanged -= OnCultureChanged;

        // Unsubscribe from hotkey events
        _hotkeyService.ToggleRecordingRequested -= OnToggleRecordingRequested;
        _hotkeyService.TogglePlaybackRequested -= OnTogglePlaybackRequested;
        _hotkeyService.TogglePauseRequested -= OnTogglePauseRequested;
        _hotkeyService.ErrorOccurred -= OnGlobalHotkeyError;
        _profileManager?.ProfileChanged -= OnProfileChanged;
        Files.LoadedMacroRemoved -= OnLoadedMacroRemoved;

        // Unsubscribe from extension status events
        _extensionNotifier?.ExtensionStatusUpdated -= OnExtensionStatusUpdated;

        // The DI scope owns child models, including the editor workspace.
    }

    private enum AppNotificationSeverity
    {
        Success,
        Warning,
        Error,
    }
}
