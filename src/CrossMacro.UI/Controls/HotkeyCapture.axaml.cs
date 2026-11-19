using Avalonia.Input;
using CrossMacro.Core;

namespace CrossMacro.UI.Controls;

public partial class HotkeyCapture : UserControl
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
    private ILocalizationService? _attachedLocalizationService;

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
            if (_isDetached)
            {
                return;
            }

            UpdateDisplayString();
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
        if (IsCapturing || _isDetached)
        {
            return;
        }

        var hotkeyService = GlobalHotkeyService;

        if (hotkeyService is null)
        {
            DisplayString = ServiceErrorDisplayText;
            return;
        }

        CancelCapture();
        IsCapturing = true;
        UpdateDisplayString();
        UpdateVisualStateClasses();
        var captureCts = new CancellationTokenSource();
        _captureCts = captureCts;

        try
        {
            var newHotkey = await hotkeyService.CaptureNextKeyAsync(captureCts.Token);
            if (!_isDetached)
            {
                ApplyCapturedHotkey(newHotkey);
            }
        }
        catch (OperationCanceledException) when (captureCts.IsCancellationRequested) { /* Empty */ }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "Hotkey capture failed");
            if (!_isDetached)
            {
                EndCapture();
            }
        }
        finally
        {
            if (ReferenceEquals(_captureCts, captureCts))
            {
                _captureCts = null;
            }

            captureCts.Dispose();
        }
    }

    private void ApplyCapturedHotkey(string newHotkey)
    {
        if (ValidationFunc is not null)
        {
            var (isValid, errorMessage) = ValidationFunc(newHotkey);
            if (!isValid)
            {
                IsValid = false;
                ErrorMessage = errorMessage;
                ScheduleValidationReset();
                EndCapture();
                return;
            }
        }

        CancelValidationResetTimer();
        ResetValidationState();
        Hotkey = newHotkey;
        HotkeyChanged?.Invoke(this, newHotkey);
        EndCapture();
    }

    private void EndCapture()
    {
        IsCapturing = false;
        UpdateDisplayString();
        UpdateVisualStateClasses();
    }

    private void ScheduleValidationReset()
    {
        CancelValidationResetTimer();
        var validationResetCts = new CancellationTokenSource();
        _validationResetCts = validationResetCts;
        _ = ResetValidationStateAfterDelayAsync(validationResetCts);
    }

    private async Task ResetValidationStateAfterDelayAsync(CancellationTokenSource validationResetCts)
    {
        try
        {
            await Task.Delay(ValidationResetDelayMs, validationResetCts.Token);
            ResetValidationState();
            UpdateVisualStateClasses();
        }
        catch (OperationCanceledException) { /* Empty */ }
        finally
        {
            if (ReferenceEquals(_validationResetCts, validationResetCts))
            {
                _validationResetCts = null;
            }

            validationResetCts.Dispose();
        }
    }

    private void ResetValidationState()
    {
        IsValid = true;
        ErrorMessage = string.Empty;
    }

    private void CancelValidationResetTimer()
    {
        _validationResetCts?.Cancel();
        _validationResetCts = null;
    }

    private void CancelCapture()
    {
        _captureCts?.Cancel();
        _captureCts = null;
    }

    private void UpdateVisualStateClasses()
    {
        HotkeyBorder.Classes.Set(CapturingClass, IsCapturing);
        HotkeyBorder.Classes.Set(InvalidClass, !IsValid);
        HotkeyBorder.Classes.Set(EmptyClass, !IsCapturing && string.IsNullOrWhiteSpace(Hotkey));
    }

    private static string CapturingDisplayText => Localization.LocalizationService.Get("HotkeyCapture_PressAKey");

    private static string EmptyDisplayText => Localization.LocalizationService.Get("HotkeyCapture_ClickToSet");

    private static string ServiceErrorDisplayText => Localization.LocalizationService.Get("HotkeyCapture_ServiceError");
}
