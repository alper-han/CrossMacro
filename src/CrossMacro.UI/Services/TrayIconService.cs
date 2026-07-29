
namespace CrossMacro.UI.Services;

/// <summary>
/// Service for managing system tray icon with Discord-like behavior
/// </summary>
public sealed class TrayIconService(
    IDesktopLifetimeContext desktopLifetimeContext,
    MainWindowViewModel viewModel,
    ILocalizationService localizationService,
    IRuntimeContext? runtimeContext = null) : ITrayIconService, IAsyncDisposable
{
    private const string TrayIconAssetScheme = "avares";
    private const string TrayIconAssetPath = "CrossMacro.UI.Core/Assets/mouse-icon.png";

    private TrayIcon? _trayIcon;
    private readonly Lock _disposeLock = new();
    private readonly IDesktopLifetimeContext _desktopLifetimeContext = desktopLifetimeContext;
    private readonly MainWindowViewModel _viewModel = viewModel;
    private readonly IRuntimeContext _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
    private readonly ILocalizationService _localizationService = localizationService;
    private Window? _mainWindow;
    private int _disposeRequested;
    private Task? _disposeTask;
    private bool _initialized;
    private bool _isExiting;
    private bool _isEnabled = true;

    private NativeMenuItem? _startRecordingItem;
    private NativeMenuItem? _startPlaybackItem;
    private NativeMenuItem? _stopItem;
    private NativeMenuItem? _showHideItem;
    private NativeMenuItem? _exitItem;

    public bool IsAvailable => Volatile.Read(ref _disposeRequested) is 0 && _trayIcon is not null;

    public static bool IsTraySupported(IRuntimeContext runtimeContext)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);
        return !runtimeContext.IsFlatpak;
    }

    /// <summary>
    /// Returns true if tray icon is supported in the current environment.
    /// Flatpak lacks StatusNotifierItem portal: https://github.com/flatpak/xdg-desktop-portal/issues/266
    /// </summary>
    public static bool IsTraySupported()
    {
        throw new InvalidOperationException("IRuntimeContext must be supplied by composition.");
    }

    public void Initialize()
    {
        if (IsDisposeRequested)
        {
            return;
        }

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            PostToUiThread(Initialize);
            return;
        }

        if (IsDisposeRequested || _initialized)
        {
            return;
        }

        try
        {
            var desktop = _desktopLifetimeContext.DesktopLifetime;
            if (desktop is not null)
            {
                _mainWindow = _desktopLifetimeContext.MainWindow;

                _mainWindow?.Closing += OnWindowClosing;

                desktop.ShutdownRequested += OnShutdownRequested;
            }

            // Try to create and initialize tray icon
            // This may fail in sandboxed environments (Flatpak) where D-Bus access is restricted
            if (!TryInitializeTrayIcon())
            {
                Log.Warning("Tray icon not available - running without system tray support");
                _isEnabled = false;
                return;
            }

            // Subscribe to hotkey changes
            _viewModel.Settings.PropertyChanged += OnSettingsPropertyChanged;
            _localizationService.CultureChanged += OnCultureChanged;
            _initialized = true;

            Log.Information("Tray icon initialized successfully");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Failed to initialize tray icon");
            _isEnabled = false;
        }
    }

    private bool TryInitializeTrayIcon()
    {
        try
        {
            // Flatpak sandbox blocks D-Bus StatusNotifierItem dynamic name registration
            // (org.kde.StatusNotifierItem-{PID}-{ID}) which cannot be permitted with wildcards.
            // See: https://github.com/flatpak/xdg-desktop-portal/issues/266
            if (_runtimeContext.IsFlatpak)
            {
                Log.Information("Tray icon disabled in Flatpak (D-Bus StatusNotifierItem not supported in sandbox)");
                return false;
            }

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri($"{TrayIconAssetScheme}://{TrayIconAssetPath}", UriKind.Absolute))),
                ToolTipText = AppConstants.AppName,
            };

            var menu = new NativeMenu();

            _showHideItem = new NativeMenuItem { Header = _localizationService["Tray_ShowHide"] };
            _showHideItem.Click += OnShowHideClicked;
            menu.Add(_showHideItem);

            menu.Add(new NativeMenuItemSeparator());

            // Use actual hotkey values from settings
            _startRecordingItem = new NativeMenuItem { Header = string.Format(_localizationService.CurrentCulture, _localizationService["Tray_StartRecording"], _viewModel.Settings.RecordingHotkey) };
            _startRecordingItem.Click += OnStartRecordingClicked;
            menu.Add(_startRecordingItem);

            _startPlaybackItem = new NativeMenuItem { Header = string.Format(_localizationService.CurrentCulture, _localizationService["Tray_StartPlayback"], _viewModel.Settings.PlaybackHotkey) };
            _startPlaybackItem.Click += OnStartPlaybackClicked;
            menu.Add(_startPlaybackItem);

            _stopItem = new NativeMenuItem { Header = string.Format(_localizationService.CurrentCulture, _localizationService["Tray_Stop"], _viewModel.Settings.PauseHotkey) };
            _stopItem.Click += OnStopClicked;
            menu.Add(_stopItem);

            menu.Add(new NativeMenuItemSeparator());

            _exitItem = new NativeMenuItem { Header = _localizationService["Tray_Exit"] };
            _exitItem.Click += OnExitClicked;
            menu.Add(_exitItem);

            _trayIcon.Menu = menu;
            _trayIcon.Clicked += OnTrayIconClicked;

            // This is where D-Bus connection is typically established
            // and may throw in Flatpak sandbox
            _trayIcon.IsVisible = true;

            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Log the specific error for debugging
            Log.Warning(ex, "Could not initialize tray icon (this is expected in Flatpak sandbox)");

            // Clean up partial initialization
            if (_trayIcon is not null)
            {
                try { _trayIcon.Dispose(); } catch (Exception disposeException) when (disposeException is not OutOfMemoryException) { /* Empty */ }
                _trayIcon = null;
            }

            return false;
        }
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RefreshMenuLabels(e.PropertyName);
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        RefreshMenuLabels();
    }

    private void RefreshMenuLabels(string? changedPropertyName = null)
    {
        if (IsDisposeRequested)
        {
            return;
        }

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            PostToUiThread(() => RefreshMenuLabels(changedPropertyName));
            return;
        }

        switch (changedPropertyName)
        {
            case nameof(_viewModel.Settings.RecordingHotkey):
                UpdateRecordingHeader();
                break;
            case nameof(_viewModel.Settings.PlaybackHotkey):
                UpdatePlaybackHeader();
                break;
            case nameof(_viewModel.Settings.PauseHotkey):
                UpdateStopHeader();
                break;
            default:
                _ = _showHideItem?.Header = _localizationService["Tray_ShowHide"];

                UpdateRecordingHeader();
                UpdatePlaybackHeader();
                UpdateStopHeader();

                _ = _exitItem?.Header = _localizationService["Tray_Exit"];
                break;
        }
    }

    private void UpdateRecordingHeader()
    {
        _ = _startRecordingItem?.Header = string.Format(_localizationService.CurrentCulture, _localizationService["Tray_StartRecording"], _viewModel.Settings.RecordingHotkey);
    }

    private void UpdatePlaybackHeader()
    {
        _ = _startPlaybackItem?.Header = string.Format(_localizationService.CurrentCulture, _localizationService["Tray_StartPlayback"], _viewModel.Settings.PlaybackHotkey);
    }

    private void UpdateStopHeader()
    {
        _ = _stopItem?.Header = string.Format(_localizationService.CurrentCulture, _localizationService["Tray_Stop"], _viewModel.Settings.PauseHotkey);
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        Log.Information("System shutdown requested");
        _isExiting = true;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_isExiting && _isEnabled)
        {
            e.Cancel = true;
            SetShutdownMode(_desktopLifetimeContext, ShutdownMode.OnExplicitShutdown);
            _mainWindow?.Hide();
            Log.Debug("Window minimized to tray");
        }
        else if (!_isEnabled)
        {
            Log.Debug("Window closing (tray disabled)");
        }
    }

    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        ToggleWindowVisibility();
    }

    private void OnShowHideClicked(object? sender, EventArgs e)
    {
        ToggleWindowVisibility();
    }

    private void ToggleWindowVisibility()
    {
        if (IsDisposeRequested)
        {
            return;
        }

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            PostToUiThread(ToggleWindowVisibility);
            return;
        }

        _mainWindow ??= _desktopLifetimeContext.MainWindow;
        if (_mainWindow is null)
        {
            Log.Warning("Tray show/hide requested but main window is unavailable");
            return;
        }

        if (_mainWindow.IsVisible)
        {
            SetShutdownMode(_desktopLifetimeContext, ShutdownMode.OnExplicitShutdown);
            _mainWindow.Hide();
            Log.Debug("Window hidden via tray icon");
        }
        else
        {
            SetShutdownMode(_desktopLifetimeContext, ShutdownMode.OnLastWindowClose);
            
            _mainWindow.Show();
            
            if (_mainWindow.WindowState is WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
            
            _mainWindow.Activate();
            _mainWindow.BringIntoView();
            Log.Debug("Window shown via tray icon");
        }
    }

    private void OnStartRecordingClicked(object? sender, EventArgs e)
    {
        try
        {
            // Access recording through the child ViewModel
            _viewModel.Recording.ToggleRecording();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Error toggling recording from tray");
        }
    }

    private void OnStartPlaybackClicked(object? sender, EventArgs e)
    {
        try
        {
            // Access playback through the child ViewModel
            _viewModel.Playback.TogglePlayback();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Error toggling playback from tray");
        }
    }

    private void OnStopClicked(object? sender, EventArgs e)
    {
        try
        {
            // Stop whatever is currently running
            if (_viewModel.Recording.IsRecording)
            {
                _ = _viewModel.Recording.StopRecording();
            }
            else if (_viewModel.Playback.IsPlaying)
            {
                _viewModel.Playback.StopPlayback();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Error stopping from tray");
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        try
        {
            _isExiting = true;

            var desktop = _desktopLifetimeContext.DesktopLifetime;
            desktop?.Shutdown();

            Log.Information("Application exiting via tray menu");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Error exiting application from tray");
        }
    }

    public void Show()
    {
        if (IsDisposeRequested)
        {
            return;
        }

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            PostToUiThread(Show);
            return;
        }

        _ = _trayIcon?.IsVisible = true;
    }

    public void Hide()
    {
        if (IsDisposeRequested)
        {
            return;
        }

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            PostToUiThread(Hide);
            return;
        }

        _ = _trayIcon?.IsVisible = false;
    }

    public void UpdateTooltip(string tooltip)
    {
        if (IsDisposeRequested)
        {
            return;
        }

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            PostToUiThread(() => UpdateTooltip(tooltip));
            return;
        }
        _ = _trayIcon?.ToolTipText = tooltip;
    }

    public void SetEnabled(bool enabled)
    {
        if (IsDisposeRequested)
        {
            return;
        }

        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            PostToUiThread(() => SetEnabled(enabled));
            return;
        }
        var isEnabled = enabled && _trayIcon is not null;
        _isEnabled = isEnabled;

        _ = _trayIcon?.IsVisible = isEnabled;

        SetShutdownMode(_desktopLifetimeContext, isEnabled && (_mainWindow?.IsVisible) is not true
            ? ShutdownMode.OnExplicitShutdown
            : ShutdownMode.OnLastWindowClose);

        Log.Information("Tray icon {Status}", isEnabled ? "enabled" : "disabled");
    }

    private static void SetShutdownMode(IDesktopLifetimeContext desktopLifetimeContext, ShutdownMode mode)
    {
        if (desktopLifetimeContext.DesktopLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = mode;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _ = StartDisposeAsync();
    }

    public ValueTask DisposeAsync() => new(StartDisposeAsync());

    private Task StartDisposeAsync()
    {
        lock (_disposeLock)
        {
            if (_disposeTask is not null)
            {
                return _disposeTask;
            }

            _ = Interlocked.Exchange(ref _disposeRequested, 1);
            _viewModel.Settings.PropertyChanged -= OnSettingsPropertyChanged;
            _localizationService.CultureChanged -= OnCultureChanged;

            _disposeTask = DisposeOnUiThreadAsync();
            return _disposeTask;
        }
    }

    private async Task DisposeOnUiThreadAsync()
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            DisposeOnUiThread();
            return;
        }

        await Avalonia.Threading.Dispatcher.UIThread
            .InvokeAsync(DisposeOnUiThread, Avalonia.Threading.DispatcherPriority.Send, CancellationToken.None);
    }

    private void DisposeOnUiThread()
    {
        _mainWindow?.Closing -= OnWindowClosing;

        if (_desktopLifetimeContext.DesktopLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested -= OnShutdownRequested;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
        _mainWindow = null;
        _startRecordingItem = null;
        _startPlaybackItem = null;
        _stopItem = null;
        _showHideItem = null;
        _exitItem = null;
        Log.Debug("Tray icon service disposed");
    }

    private bool IsDisposeRequested => Volatile.Read(ref _disposeRequested) is not 0;

    private void PostToUiThread(Action action)
    {
        if (IsDisposeRequested)
        {
            return;
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!IsDisposeRequested)
            {
                action();
            }
        });
    }
}
