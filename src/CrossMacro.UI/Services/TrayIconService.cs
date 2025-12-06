using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using CrossMacro.UI.ViewModels;
using Serilog;

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

    public TrayIconService(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
    }

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
            }

            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://CrossMacro.UI/Assets/mouse-icon.png"))),
                ToolTipText = "CrossMacro",
                IsVisible = true
            };

            var menu = new NativeMenu();
            
            var showHideItem = new NativeMenuItem { Header = "Show/Hide" };
            showHideItem.Click += OnShowHideClicked;
            menu.Add(showHideItem);
            
            menu.Add(new NativeMenuItemSeparator());
            
            var startRecordingItem = new NativeMenuItem { Header = "Start Recording (F8)" };
            startRecordingItem.Click += OnStartRecordingClicked;
            menu.Add(startRecordingItem);
            
            var startPlaybackItem = new NativeMenuItem { Header = "Start Playback (F9)" };
            startPlaybackItem.Click += OnStartPlaybackClicked;
            menu.Add(startPlaybackItem);
            
            var stopItem = new NativeMenuItem { Header = "Stop (F10)" };
            stopItem.Click += OnStopClicked;
            menu.Add(stopItem);
            
            menu.Add(new NativeMenuItemSeparator());
            
            var exitItem = new NativeMenuItem { Header = "Exit" };
            exitItem.Click += OnExitClicked;
            menu.Add(exitItem);
            
            _trayIcon.Menu = menu;
            
            _trayIcon.Clicked += OnTrayIconClicked;
            
            Log.Information("Tray icon initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize tray icon");
        }
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
        
        _trayIcon?.Dispose();
        Log.Debug("Tray icon service disposed");
    }
}
