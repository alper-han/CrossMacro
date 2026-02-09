using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using CrossMacro.UI.ViewModels;
using Serilog;
using CrossMacro.Core;

namespace CrossMacro.UI.Services;

/// <summary>
/// Service for managing system tray icon with Discord-like behavior
/// </summary>
public class TrayIconService : ITrayIconService
{
    private TrayIcon? _trayIcon;
    private readonly MainWindowViewModel _viewModel;
    private Window? _mainWindow;
    private bool _isExiting;
    private bool _isEnabled = true;
    
    private NativeMenuItem? _startRecordingItem;
    private NativeMenuItem? _startPlaybackItem;
    private NativeMenuItem? _stopItem;

    public TrayIconService(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    private static bool IsFlatpakEnvironment()
    {
        // Check for Flatpak environment variables
        var flatpakId = Environment.GetEnvironmentVariable("FLATPAK_ID");
        if (!string.IsNullOrEmpty(flatpakId))
            return true;

        var crossmacroFlatpak = Environment.GetEnvironmentVariable("CROSSMACRO_FLATPAK");
        if (crossmacroFlatpak == "1")
            return true;

        // Check if running inside Flatpak sandbox
        return System.IO.File.Exists("/.flatpak-info");
    }

    /// <summary>
    /// Returns true if tray icon is supported in the current environment.
    /// Flatpak lacks StatusNotifierItem portal: https://github.com/flatpak/xdg-desktop-portal/issues/266
    /// </summary>
    public static bool IsTraySupported() => !IsFlatpakEnvironment();

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
            if (IsFlatpakEnvironment())
            {
                Log.Information("Tray icon disabled in Flatpak (D-Bus StatusNotifierItem not supported in sandbox)");
                return false;
            }

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://CrossMacro.UI/Assets/mouse-icon.png"))),
                ToolTipText = AppConstants.AppName
            };

            var menu = new NativeMenu();

            var showHideItem = new NativeMenuItem { Header = "Show/Hide" };
            showHideItem.Click += OnShowHideClicked;
            menu.Add(showHideItem);

            menu.Add(new NativeMenuItemSeparator());

            // Use actual hotkey values from settings
            _startRecordingItem = new NativeMenuItem { Header = $"Start Recording ({_viewModel.Settings.RecordingHotkey})" };
            _startRecordingItem.Click += OnStartRecordingClicked;
            menu.Add(_startRecordingItem);

            _startPlaybackItem = new NativeMenuItem { Header = $"Start Playback ({_viewModel.Settings.PlaybackHotkey})" };
            _startPlaybackItem.Click += OnStartPlaybackClicked;
            menu.Add(_startPlaybackItem);

            _stopItem = new NativeMenuItem { Header = $"Stop ({_viewModel.Settings.PauseHotkey})" };
            _stopItem.Click += OnStopClicked;
            menu.Add(_stopItem);

            menu.Add(new NativeMenuItemSeparator());

            var exitItem = new NativeMenuItem { Header = "Exit" };
            exitItem.Click += OnExitClicked;
            menu.Add(exitItem);

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
        switch (e.PropertyName)
        {
            case nameof(_viewModel.Settings.RecordingHotkey):
                if (_startRecordingItem != null)
                    _startRecordingItem.Header = $"Start Recording ({_viewModel.Settings.RecordingHotkey})";
                break;
            case nameof(_viewModel.Settings.PlaybackHotkey):
                if (_startPlaybackItem != null)
                    _startPlaybackItem.Header = $"Start Playback ({_viewModel.Settings.PlaybackHotkey})";
                break;
            case nameof(_viewModel.Settings.PauseHotkey):
                if (_stopItem != null)
                    _stopItem.Header = $"Stop ({_viewModel.Settings.PauseHotkey})";
                break;
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
            _mainWindow.Hide();
            Log.Debug("Window hidden via tray icon");
        }
        else
        {
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
        _isEnabled = enabled;
        
        if (_trayIcon != null)
        {
            _trayIcon.IsVisible = enabled;
            Log.Information("Tray icon {Status}", enabled ? "enabled" : "disabled");
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
        
        _trayIcon?.Dispose();
        Log.Debug("Tray icon service disposed");
    }
}
