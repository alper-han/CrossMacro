using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using CrossMacro.UI.Localization;
using CrossMacro.UI.ViewModels;
using Serilog;
using CrossMacro.Core;
using CrossMacro.Core.Services;

namespace CrossMacro.UI.Services;

/// <summary>
/// Service for managing system tray icon with Discord-like behavior
/// </summary>
public class TrayIconService : ITrayIconService
{
    private TrayIcon? _trayIcon;
    private readonly MainWindowViewModel _viewModel;
    private readonly IRuntimeContext _runtimeContext;
    private readonly ILocalizationService _localizationService;
    private Window? _mainWindow;
    private bool _isExiting;
    private bool _isEnabled = true;
    
    private NativeMenuItem? _startRecordingItem;
    private NativeMenuItem? _startPlaybackItem;
    private NativeMenuItem? _stopItem;
    private NativeMenuItem? _showHideItem;
    private NativeMenuItem? _exitItem;

    public TrayIconService(MainWindowViewModel viewModel, ILocalizationService localizationService, IRuntimeContext? runtimeContext = null)
    {
        _viewModel = viewModel;
        _localizationService = localizationService;
        _runtimeContext = runtimeContext ?? new RuntimeContext();
    }

    public bool IsAvailable => _trayIcon != null;

    public static bool IsTraySupported(IRuntimeContext runtimeContext)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);
        return !runtimeContext.IsFlatpak;
    }

    /// <summary>
    /// Returns true if tray icon is supported in the current environment.
    /// Flatpak lacks StatusNotifierItem portal: https://github.com/flatpak/xdg-desktop-portal/issues/266
    /// </summary>
    public static bool IsTraySupported() => IsTraySupported(new RuntimeContext());

    public void Initialize()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _mainWindow = desktop.MainWindow;

                if (_mainWindow != null)
                {
                    _mainWindow.Closing += OnWindowClosing;
                }

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

            Log.Information("Tray icon initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize tray icon");
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
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://CrossMacro.UI.Core/Assets/mouse-icon.png"))),
                ToolTipText = AppConstants.AppName
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
        catch (Exception ex)
        {
            // Log the specific error for debugging
            Log.Warning(ex, "Could not initialize tray icon (this is expected in Flatpak sandbox)");

            // Clean up partial initialization
            if (_trayIcon != null)
            {
                try { _trayIcon.Dispose(); } catch { }
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
                if (_showHideItem != null)
                {
                    _showHideItem.Header = _localizationService["Tray_ShowHide"];
                }

                UpdateRecordingHeader();
                UpdatePlaybackHeader();
                UpdateStopHeader();

                if (_exitItem != null)
                {
                    _exitItem.Header = _localizationService["Tray_Exit"];
                }
                break;
        }
    }

    private void UpdateRecordingHeader()
    {
        if (_startRecordingItem != null)
        {
            _startRecordingItem.Header = string.Format(_localizationService.CurrentCulture, _localizationService["Tray_StartRecording"], _viewModel.Settings.RecordingHotkey);
        }
    }

    private void UpdatePlaybackHeader()
    {
        if (_startPlaybackItem != null)
        {
            _startPlaybackItem.Header = string.Format(_localizationService.CurrentCulture, _localizationService["Tray_StartPlayback"], _viewModel.Settings.PlaybackHotkey);
        }
    }

    private void UpdateStopHeader()
    {
        if (_stopItem != null)
        {
            _stopItem.Header = string.Format(_localizationService.CurrentCulture, _localizationService["Tray_Stop"], _viewModel.Settings.PauseHotkey);
        }
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
            SetShutdownMode(ShutdownMode.OnExplicitShutdown);
            if (_mainWindow != null)
            {
                _mainWindow.ShowInTaskbar = false;
            }
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
        if (_mainWindow == null) return;

        if (_mainWindow.IsVisible)
        {
            SetShutdownMode(ShutdownMode.OnExplicitShutdown);
            _mainWindow.ShowInTaskbar = false;
            _mainWindow.Hide();
            Log.Debug("Window hidden via tray icon");
        }
        else
        {
            SetShutdownMode(ShutdownMode.OnLastWindowClose);
            _mainWindow.ShowInTaskbar = true;
            _mainWindow.Show();
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
        catch (Exception ex)
        {
            Log.Error(ex, "Error toggling recording from tray");
        }
    }

    private void OnStartPlaybackClicked(object? sender, EventArgs e)
    {
        try
        {
            // Access playback through the child ViewModel
            _viewModel.Playback.TogglePlayback();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error toggling playback from tray");
        }
    }

    private void OnStopClicked(object? sender, EventArgs e)
    {
        try
        {
            // Stop whatever is currently running
            if (_viewModel.Recording.IsRecording)
            {
                _viewModel.Recording.StopRecording();
            }
            else if (_viewModel.Playback.IsPlaying)
            {
                _viewModel.Playback.StopPlayback();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error stopping from tray");
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        try
        {
            _isExiting = true;
            
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            
            Log.Information("Application exiting via tray menu");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error exiting application from tray");
        }
    }

    public void Show()
    {
        if (_trayIcon != null)
        {
            _trayIcon.IsVisible = true;
        }
    }

    public void Hide()
    {
        if (_trayIcon != null)
        {
            _trayIcon.IsVisible = false;
        }
    }

    public void UpdateTooltip(string tooltip)
    {
        if (_trayIcon != null)
        {
            _trayIcon.ToolTipText = tooltip;
        }
    }

    public void SetEnabled(bool enabled)
    {
        var isEnabled = enabled && _trayIcon != null;
        _isEnabled = isEnabled;

        if (_trayIcon != null)
        {
            _trayIcon.IsVisible = isEnabled;
        }

        SetShutdownMode(isEnabled && _mainWindow?.IsVisible != true
            ? ShutdownMode.OnExplicitShutdown
            : ShutdownMode.OnLastWindowClose);

        Log.Information("Tray icon {Status}", isEnabled ? "enabled" : "disabled");
    }

    private static void SetShutdownMode(ShutdownMode mode)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = mode;
        }
    }

    public void Dispose()
    {
        if (_mainWindow != null)
        {
            _mainWindow.Closing -= OnWindowClosing;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested -= OnShutdownRequested;
        }
        
        _viewModel.Settings.PropertyChanged -= OnSettingsPropertyChanged;
        _localizationService.CultureChanged -= OnCultureChanged;
        
        _trayIcon?.Dispose();
        Log.Debug("Tray icon service disposed");
    }
}
