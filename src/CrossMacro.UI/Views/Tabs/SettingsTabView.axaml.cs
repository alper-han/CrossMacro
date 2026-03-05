using Avalonia.Controls;
using CrossMacro.UI.Controls;
using CrossMacro.UI.ViewModels;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.UI.Views.Tabs;

public partial class SettingsTabView : UserControl
{
    private HotkeyCapture? _recordingHotkeyCapture;
    private HotkeyCapture? _playbackHotkeyCapture;
    private HotkeyCapture? _pauseHotkeyCapture;
    private Border? _toastNotification;
    private TextBlock? _toastMessage;
    private CancellationTokenSource? _toastCts;
    
    public SettingsTabView()
    {
        InitializeComponent();
        
        // Wire up validation after the controls are loaded
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }
    
    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Get references to the HotkeyCapture controls
        _recordingHotkeyCapture = this.FindControl<HotkeyCapture>("RecordingHotkeyCapture");
        _playbackHotkeyCapture = this.FindControl<HotkeyCapture>("PlaybackHotkeyCapture");
        _pauseHotkeyCapture = this.FindControl<HotkeyCapture>("PauseHotkeyCapture");
        
        // Get references to toast notification elements
        _toastNotification = this.FindControl<Border>("ToastNotification");
        _toastMessage = this.FindControl<TextBlock>("ToastMessage");
        ResetToastState();
        
        var viewModel = DataContext as SettingsViewModel;
        
        if (_recordingHotkeyCapture != null && viewModel != null)
        {
            _recordingHotkeyCapture.ValidationFunc = (newHotkey) =>
            {
                if (newHotkey == viewModel.PlaybackHotkey)
                {
                    ShowToast("This hotkey is already assigned to Playback");
                    return (false, "This hotkey is already assigned to Playback");
                }
                if (newHotkey == viewModel.PauseHotkey)
                {
                    ShowToast("This hotkey is already assigned to Pause");
                    return (false, "This hotkey is already assigned to Pause");
                }
                return (true, string.Empty);
            };
        }
        
        if (_playbackHotkeyCapture != null && viewModel != null)
        {
            _playbackHotkeyCapture.ValidationFunc = (newHotkey) =>
            {
                if (newHotkey == viewModel.RecordingHotkey)
                {
                    ShowToast("This hotkey is already assigned to Recording");
                    return (false, "This hotkey is already assigned to Recording");
                }
                if (newHotkey == viewModel.PauseHotkey)
                {
                    ShowToast("This hotkey is already assigned to Pause");
                    return (false, "This hotkey is already assigned to Pause");
                }
                return (true, string.Empty);
            };
        }
        
        if (_pauseHotkeyCapture != null && viewModel != null)
        {
            _pauseHotkeyCapture.ValidationFunc = (newHotkey) =>
            {
                if (newHotkey == viewModel.RecordingHotkey)
                {
                    ShowToast("This hotkey is already assigned to Recording");
                    return (false, "This hotkey is already assigned to Recording");
                }
                if (newHotkey == viewModel.PlaybackHotkey)
                {
                    ShowToast("This hotkey is already assigned to Playback");
                    return (false, "This hotkey is already assigned to Playback");
                }
                return (true, string.Empty);
            };
        }
    }
    
    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CancelToastTimer();
        ResetToastState();
    }

    private async void ShowToast(string message)
    {
        if (_toastNotification == null || _toastMessage == null)
            return;

        CancelToastTimer();
        var toastCts = new CancellationTokenSource();
        _toastCts = toastCts;
        var token = toastCts.Token;
            
        _toastMessage.Text = message;
        _toastNotification.IsVisible = true;
        _toastNotification.Opacity = 1.0;

        try
        {
            await Task.Delay(2000, token);

            if (token.IsCancellationRequested || _toastNotification == null)
            {
                return;
            }

            _toastNotification.Opacity = 0.0;

            // Wait for fade animation to complete before hiding
            await Task.Delay(300, token);

            if (token.IsCancellationRequested || _toastNotification == null)
            {
                return;
            }

            ResetToastState();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Settings toast flow failed");
        }
        finally
        {
            if (ReferenceEquals(_toastCts, toastCts))
            {
                toastCts.Dispose();
                _toastCts = null;
            }
        }
    }

    private void CancelToastTimer()
    {
        if (_toastCts == null)
        {
            return;
        }

        _toastCts.Cancel();
        _toastCts.Dispose();
        _toastCts = null;
    }

    private void ResetToastState()
    {
        if (_toastNotification != null)
        {
            _toastNotification.IsVisible = false;
            _toastNotification.Opacity = 0.0;
        }

        if (_toastMessage != null)
        {
            _toastMessage.Text = string.Empty;
        }
    }
}
