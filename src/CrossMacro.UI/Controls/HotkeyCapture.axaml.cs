
namespace CrossMacro.UI.Controls;

public partial class HotkeyCapture : UserControl, IDisposable
{
    public static readonly StyledProperty<string> HotkeyProperty =
        AvaloniaProperty.Register<HotkeyCapture, string>(nameof(Hotkey), AppConstants.DefaultRecordingHotkey);

    public static readonly StyledProperty<ILocalizationService?> LocalizationServiceProperty =
        AvaloniaProperty.Register<HotkeyCapture, ILocalizationService?>(nameof(LocalizationService));

    public static readonly StyledProperty<IGlobalHotkeyService?> GlobalHotkeyServiceProperty =
        AvaloniaProperty.Register<HotkeyCapture, IGlobalHotkeyService?>(nameof(GlobalHotkeyService));

    public static readonly DirectProperty<HotkeyCapture, bool> IsCapturingProperty =
        AvaloniaProperty.RegisterDirect<HotkeyCapture, bool>(
            nameof(IsCapturing),
            o => o.IsCapturing);

    public static readonly DirectProperty<HotkeyCapture, bool> IsValidProperty =
        AvaloniaProperty.RegisterDirect<HotkeyCapture, bool>(
            nameof(IsValid),
            o => o.IsValid);

    public static readonly DirectProperty<HotkeyCapture, string> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<HotkeyCapture, string>(
            nameof(ErrorMessage),
            o => o.ErrorMessage);
    private const string CapturingClass = "capturing";
    private const string InvalidClass = "invalid";
    private const string EmptyClass = "empty";
    private const int ValidationResetDelayMs = 2000;
    private CancellationTokenSource? _validationResetCts;
    private CancellationTokenSource? _captureCts;
    private bool _isDetached = true;
    private bool _disposed;
    private ILocalizationService? _attachedLocalizationService;
    private readonly Lock _validationResetLock = new();
    private readonly Lock _captureLock = new();

    public ILocalizationService? LocalizationService
    {
        get => GetValue(LocalizationServiceProperty);
        set => SetValue(LocalizationServiceProperty, value);
    }

    public IGlobalHotkeyService? GlobalHotkeyService
    {
        get => GetValue(GlobalHotkeyServiceProperty);
        set => SetValue(GlobalHotkeyServiceProperty, value);
    }

    public string Hotkey
    {
        get => GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    public bool IsCapturing
    {
        get;
        private set => SetAndRaise(IsCapturingProperty, ref field, value);
    }

    public bool IsValid
    {
        get;
        private set => SetAndRaise(IsValidProperty, ref field, value);
    } = true;

    public string ErrorMessage
    {
        get;
        private set => SetAndRaise(ErrorMessageProperty, ref field, value);
    } = string.Empty;

    public event EventHandler<string>? HotkeyChanged;

    public Func<string, (bool isValid, string errorMessage)>? ValidationFunc { get; set; }

    public static readonly DirectProperty<HotkeyCapture, string> DisplayStringProperty =
        AvaloniaProperty.RegisterDirect<HotkeyCapture, string>(
            nameof(DisplayString),
            o => o.DisplayString);

    public string DisplayString
    {
        get;
        private set => SetAndRaise(DisplayStringProperty, ref field, value);
    } = AppConstants.DefaultRecordingHotkey;

    public HotkeyCapture()
    {
        InitializeComponent();
        UpdateDisplayString();
        UpdateVisualStateClasses();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == HotkeyProperty)
        {
            UpdateDisplayString();
            UpdateVisualStateClasses();
        }
        else if (change.Property == LocalizationServiceProperty)
        {
            AttachLocalizationService(GetValue(LocalizationServiceProperty));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isDetached = true;
        DetachLocalizationService();
        CancelCapture();
        CancelValidationResetTimer();
        IsCapturing = false;
        ResetValidationState();
        UpdateDisplayString();
        UpdateVisualStateClasses();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isDetached = false;
        UpdateDisplayString();
        base.OnAttachedToVisualTree(e);
    }

    private void DetachLocalizationService()
    {
        _attachedLocalizationService?.CultureChanged -= OnCultureChanged;
        _attachedLocalizationService = null;
    }

    private void AttachLocalizationService(ILocalizationService? localizationService)
    {
        if (ReferenceEquals(_attachedLocalizationService, localizationService))
        {
            return;
        }

        DetachLocalizationService();
        _attachedLocalizationService = localizationService;
        _attachedLocalizationService?.CultureChanged += OnCultureChanged;

        UpdateDisplayString();
        UpdateVisualStateClasses();
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_isDetached || _disposed)
            {
                return;
            }

            UpdateDisplayString();
            UpdateVisualStateClasses();
        });
    }

    private void UpdateDisplayString()
    {
        if (IsCapturing)
        {
            DisplayString = CapturingDisplayText;
            return;
        }

        DisplayString = string.IsNullOrWhiteSpace(Hotkey) ? EmptyDisplayText : Hotkey;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        _ = StartCaptureAsync();
    }

    private async Task StartCaptureAsync()
    {
        if (IsCapturing || _isDetached || _disposed)
        {
            return;
        }

        var hotkeyService = GlobalHotkeyService;

        if (hotkeyService is null)
        {
            DisplayString = ServiceErrorDisplayText;
            return;
        }

        IsCapturing = true;
        UpdateDisplayString();
        UpdateVisualStateClasses();
        CancelCapture();
        var captureCts = new CancellationTokenSource();
        _captureCts = captureCts;
        var captureToken = captureCts.Token;

        try
        {
            // Capture directly from the service (bypassing UI/OS filtering)
            var newHotkey = await hotkeyService.CaptureNextKeyAsync(captureToken).ConfigureAwait(false);

            // Update on UI thread
            Dispatcher.UIThread.Post(() =>
            {
                if (_isDetached)
                {
                    return;
                }

                // Validate the new hotkey if validation function is provided
                if (ValidationFunc is not null)
                {
                    var (isValid, errorMessage) = ValidationFunc(newHotkey);

                    if (!isValid)
                    {
                        // Show error state briefly
                        IsValid = false;
                        ErrorMessage = errorMessage;
                        UpdateVisualStateClasses();
                        ScheduleValidationReset();

                        IsCapturing = false;
                        UpdateDisplayString();
                        UpdateVisualStateClasses();
                        return;
                    }
                }

                // Valid hotkey - update
                CancelValidationResetTimer();
                ResetValidationState();
                Hotkey = newHotkey;
                HotkeyChanged?.Invoke(this, newHotkey);
                IsCapturing = false;
                UpdateDisplayString();
                UpdateVisualStateClasses();
            });
        }
        catch (OperationCanceledException) when (captureToken.IsCancellationRequested) { /* Empty */ }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_isDetached)
                {
                    return;
                }

                IsCapturing = false;
                UpdateDisplayString();
                UpdateVisualStateClasses();

                Log.LogError(ex, "Capture failed");
            });
        }
        finally
        {
            lock (_captureLock)
            {
                if (ReferenceEquals(_captureCts, captureCts))
                {
                    _captureCts = null;
                }

                captureCts.Dispose();
            }
        }
    }

    private void ScheduleValidationReset()
    {
        if (_isDetached || _disposed)
        {
            return;
        }

        CancelValidationResetTimer();
        var validationResetCts = new CancellationTokenSource();
        lock (_validationResetLock)
        {
            _validationResetCts = validationResetCts;
        }

        _ = ResetValidationStateAfterDelayAsync(validationResetCts);
    }

    private async Task ResetValidationStateAfterDelayAsync(CancellationTokenSource validationResetCts)
    {
        var token = validationResetCts.Token;
        try
        {
            await Task.Delay(ValidationResetDelayMs, token).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || _isDetached || _disposed)
                {
                    return;
                }

                ResetValidationState();
                UpdateVisualStateClasses();
            });
        }
        catch (OperationCanceledException) { /* Empty */ }
        finally
        {
            lock (_validationResetLock)
            {
                if (ReferenceEquals(_validationResetCts, validationResetCts))
                {
                    _validationResetCts = null;
                }

                validationResetCts.Dispose();
            }
        }
    }

    private void ResetValidationState()
    {
        IsValid = true;
        ErrorMessage = string.Empty;
    }

    private void CancelValidationResetTimer()
    {
        lock (_validationResetLock)
        {
            if (_validationResetCts is null)
            {
                return;
            }

            _validationResetCts.Cancel();
            _validationResetCts = null;
        }
    }

    private void CancelCapture()
    {
        lock (_captureLock)
        {
            if (_captureCts is null)
            {
                return;
            }

            _captureCts.Cancel();
            _captureCts = null;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || _disposed)
        {
            return;
        }

        _disposed = true;
        _isDetached = true;
        DetachLocalizationService();
        CancelCapture();
        CancelValidationResetTimer();
    }

    private void UpdateVisualStateClasses()
    {
        Classes.Set(CapturingClass, IsCapturing);
        Classes.Set(InvalidClass, !IsValid);
        Classes.Set(EmptyClass, !IsCapturing && string.IsNullOrWhiteSpace(Hotkey));

        if (HotkeyBorder is not null)
        {
            HotkeyBorder.Classes.Set(CapturingClass, IsCapturing);
            HotkeyBorder.Classes.Set(InvalidClass, !IsValid);
            HotkeyBorder.Classes.Set(EmptyClass, !IsCapturing && string.IsNullOrWhiteSpace(Hotkey));
        }
    }

    private string CapturingDisplayText => _attachedLocalizationService?["HotkeyCapture_PressAKey"] ?? "Press a key...";

    private string EmptyDisplayText => _attachedLocalizationService?["HotkeyCapture_ClickToSet"] ?? "Click to set hotkey";

    private string ServiceErrorDisplayText => _attachedLocalizationService?["HotkeyCapture_ServiceError"] ?? "Service Error";
}
