
namespace CrossMacro.UI.Views.Tabs;

public partial class SettingsTabView : UserControl
{
    private HotkeyCapture? _recordingHotkeyCapture;
    private HotkeyCapture? _playbackHotkeyCapture;
    private HotkeyCapture? _pauseHotkeyCapture;
    private SettingsViewModel? _profileToastViewModel;
    private Border? _toastNotification;
    private TextBlock? _toastMessage;
    private CancellationTokenSource? _toastCts;
    private int _toastLifecycleVersion;

    public SettingsTabView()
    {
        InitializeComponent();

        // Wire up validation after the controls are loaded
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _ = Interlocked.Increment(ref _toastLifecycleVersion);

        // Get references to the HotkeyCapture controls
        _recordingHotkeyCapture = this.FindControl<HotkeyCapture>("RecordingHotkeyCapture");
        _playbackHotkeyCapture = this.FindControl<HotkeyCapture>("PlaybackHotkeyCapture");
        _pauseHotkeyCapture = this.FindControl<HotkeyCapture>("PauseHotkeyCapture");

        // Get references to toast notification elements
        _toastNotification = this.FindControl<Border>("ToastNotification");
        _toastMessage = this.FindControl<TextBlock>("ToastMessage");
        ResetToastState();
        _profileToastViewModel?.ProfileOperationFailed -= OnProfileOperationFailed;
        _profileToastViewModel = null;

        var viewModel = DataContext as SettingsViewModel;
        if (viewModel is not null)
        {
            _profileToastViewModel = viewModel;
            _profileToastViewModel.ProfileOperationFailed += OnProfileOperationFailed;
        }

        if (viewModel is not null)
        {
            // Validation rules live in the ViewModel (localized); the view only surfaces the
            // failure as a toast.
            WireHotkeyValidation(_recordingHotkeyCapture, viewModel.ValidateRecordingHotkey);
            WireHotkeyValidation(_playbackHotkeyCapture, viewModel.ValidatePlaybackHotkey);
            WireHotkeyValidation(_pauseHotkeyCapture, viewModel.ValidatePauseHotkey);
        }
    }

    private void WireHotkeyValidation(
        HotkeyCapture? capture,
        Func<string, (bool IsValid, string ErrorMessage)> validate)
    {
        if (capture is null)
        {
            return;
        }

        capture.ValidationFunc = newHotkey =>
        {
            var result = validate(newHotkey);
            if (!result.IsValid)
            {
                _ = ShowToastAsync(result.ErrorMessage);
            }

            return result;
        };
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _ = Interlocked.Increment(ref _toastLifecycleVersion);
        _profileToastViewModel?.ProfileOperationFailed -= OnProfileOperationFailed;
        _profileToastViewModel = null;

        CancelToastTimer();
        ResetToastState();
    }

    private void OnProfileOperationFailed(object? sender, string message)
    {
        _ = ShowToastAsync(message);
    }

    private async Task ShowToastAsync(string message)
    {
        var lifecycleVersion = Volatile.Read(ref _toastLifecycleVersion);
        var toastCts = await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(BeginToastOnUiThread);
        if (toastCts is null)
        {
            return;
        }

        var token = toastCts.Token;

        try
        {
            await Task.Delay(2000, token).ConfigureAwait(false);
            if (!await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(BeginToastFadeOnUiThread))
            {
                return;
            }

            await Task.Delay(300, token).ConfigureAwait(false);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(CompleteToastOnUiThread);
        }
        catch (OperationCanceledException) { /* Empty */ }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "Settings toast flow failed");
        }
        finally
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(CompleteToastOnUiThread);
        }

        CancellationTokenSource? BeginToastOnUiThread() => BeginToast(message, lifecycleVersion);

        bool BeginToastFadeOnUiThread() => BeginToastFade(toastCts);

        void CompleteToastOnUiThread() => CompleteToast(toastCts);
    }

    private CancellationTokenSource? BeginToast(string message, int lifecycleVersion)
    {
        if (lifecycleVersion != Volatile.Read(ref _toastLifecycleVersion) || !IsLoaded || _toastNotification is null || _toastMessage is null)
        {
            return null;
        }

        CancelToastTimer();
        var toastCts = new CancellationTokenSource();
        _toastCts = toastCts;
        _toastMessage.Text = message;
        _toastNotification.IsVisible = true;
        _toastNotification.Opacity = 1.0;
        return toastCts;
    }

    private bool BeginToastFade(CancellationTokenSource toastCts)
    {
        if (toastCts.IsCancellationRequested || !ReferenceEquals(_toastCts, toastCts) || _toastNotification is null)
        {
            return false;
        }

        _toastNotification.Opacity = 0.0;
        return true;
    }

    private void CompleteToast(CancellationTokenSource toastCts)
    {
        if (!ReferenceEquals(_toastCts, toastCts))
        {
            return;
        }

        if (!toastCts.IsCancellationRequested)
        {
            ResetToastState();
        }

        toastCts.Dispose();
        _toastCts = null;
    }

    private void CancelToastTimer()
    {
        if (_toastCts is null)
        {
            return;
        }

        _toastCts.Cancel();
        _toastCts.Dispose();
        _toastCts = null;
    }

    private void ResetToastState()
    {
        if (_toastNotification is not null)
        {
            _toastNotification.IsVisible = false;
            _toastNotification.Opacity = 0.0;
        }

        _ = _toastMessage?.Text = string.Empty;
    }
}
