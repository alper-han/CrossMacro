using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
using Microsoft.Extensions.DependencyInjection;
using CrossMacro.Core;
using Serilog;

namespace CrossMacro.UI.Controls;

public partial class HotkeyCapture : UserControl
{
    public static readonly StyledProperty<string> HotkeyProperty =
        AvaloniaProperty.Register<HotkeyCapture, string>(nameof(Hotkey), AppConstants.DefaultRecordingHotkey);

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

    private bool _isCapturing;
    private bool _isValid = true;
    private string _errorMessage = string.Empty;
    private const string CapturingClass = "capturing";
    private const string InvalidClass = "invalid";
    private const string EmptyClass = "empty";
    private const int ValidationResetDelayMs = 2000;
    private CancellationTokenSource? _validationResetCts;
    private CancellationTokenSource? _captureCts;
    private bool _isDetached = true;
    private ILocalizationService? _localizationService;

    public string Hotkey
    {
        get => GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        private set => SetAndRaise(IsCapturingProperty, ref _isCapturing, value);
    }

    public bool IsValid
    {
        get => _isValid;
        private set => SetAndRaise(IsValidProperty, ref _isValid, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetAndRaise(ErrorMessageProperty, ref _errorMessage, value);
    }

    public event EventHandler<string>? HotkeyChanged;

    public Func<string, (bool isValid, string errorMessage)>? ValidationFunc { get; set; }

    public static readonly DirectProperty<HotkeyCapture, string> DisplayStringProperty =
        AvaloniaProperty.RegisterDirect<HotkeyCapture, string>(
            nameof(DisplayString),
            o => o.DisplayString);

    private string _displayString = AppConstants.DefaultRecordingHotkey;

    public string DisplayString
    {
        get => _displayString;
        private set => SetAndRaise(DisplayStringProperty, ref _displayString, value);
    }

    public HotkeyCapture()
    {
        InitializeComponent();
        UpdateDisplayString();
        UpdateVisualStateClasses();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HotkeyProperty)
        {
            UpdateDisplayString();
            UpdateVisualStateClasses();
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
        AttachLocalizationService();
        UpdateDisplayString();
        base.OnAttachedToVisualTree(e);
    }

    private void AttachLocalizationService()
    {
        var localizationService = (Application.Current as App)?.Services?.GetService<ILocalizationService>();
        if (ReferenceEquals(_localizationService, localizationService))
        {
            return;
        }

        DetachLocalizationService();
        _localizationService = localizationService;
        if (_localizationService != null)
        {
            _localizationService.CultureChanged += OnCultureChanged;
        }
    }

    private void DetachLocalizationService()
    {
        if (_localizationService != null)
        {
            _localizationService.CultureChanged -= OnCultureChanged;
            _localizationService = null;
        }
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

    private async void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        await StartCaptureAsync();
    }

    private async Task StartCaptureAsync()
    {
        if (IsCapturing || _isDetached) return;

        // Resolve service from the public app service provider.
        var serviceProvider = (Application.Current as App)?.Services;
        var hotkeyService = serviceProvider?.GetService<IGlobalHotkeyService>();

        if (hotkeyService == null)
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
            var newHotkey = await hotkeyService.CaptureNextKeyAsync(captureToken);
            
            // Update on UI thread
            Dispatcher.UIThread.Post(() =>
            {
                if (_isDetached)
                {
                    return;
                }

                // Validate the new hotkey if validation function is provided
                if (ValidationFunc != null)
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
        catch (OperationCanceledException) when (captureToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
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
                
                Log.Error(ex, "Capture failed");
            });
        }
        finally
        {
            if (ReferenceEquals(_captureCts, captureCts))
            {
                _captureCts.Dispose();
                _captureCts = null;
            }
        }
    }

    private void ScheduleValidationReset()
    {
        if (_isDetached)
        {
            return;
        }

        CancelValidationResetTimer();
        _validationResetCts = new CancellationTokenSource();
        _ = ResetValidationStateAfterDelayAsync(_validationResetCts.Token);
    }

    private async Task ResetValidationStateAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(ValidationResetDelayMs, token);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || _isDetached)
                {
                    return;
                }

                ResetValidationState();
                UpdateVisualStateClasses();
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ResetValidationState()
    {
        IsValid = true;
        ErrorMessage = string.Empty;
    }

    private void CancelValidationResetTimer()
    {
        if (_validationResetCts == null)
        {
            return;
        }

        _validationResetCts.Cancel();
        _validationResetCts.Dispose();
        _validationResetCts = null;
    }

    private void CancelCapture()
    {
        if (_captureCts == null)
        {
            return;
        }

        _captureCts.Cancel();
        _captureCts.Dispose();
        _captureCts = null;
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

    private string CapturingDisplayText => _localizationService?["HotkeyCapture_PressAKey"] ?? "Press a key...";

    private string EmptyDisplayText => _localizationService?["HotkeyCapture_ClickToSet"] ?? "Click to set hotkey";

    private string ServiceErrorDisplayText => _localizationService?["HotkeyCapture_ServiceError"] ?? "Service Error";
}
