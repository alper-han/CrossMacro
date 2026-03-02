using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CrossMacro.Core.Services;
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

    private void UpdateDisplayString()
    {
        if (IsCapturing)
        {
            DisplayString = "Press a key...";
            return;
        }

        DisplayString = string.IsNullOrWhiteSpace(Hotkey) ? "Click to set hotkey" : Hotkey;
    }

    private async void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        await StartCaptureAsync();
    }

    private async Task StartCaptureAsync()
    {
        if (IsCapturing) return;

        // Resolve service
        var app = Application.Current as App;
        var serviceProvider = typeof(App).GetField("_serviceProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(app) as IServiceProvider;
        var hotkeyService = serviceProvider?.GetService<IGlobalHotkeyService>();

        if (hotkeyService == null)
        {
            DisplayString = "Service Error";
            return;
        }

        IsCapturing = true;
        UpdateDisplayString();
        UpdateVisualStateClasses();

        try
        {
            // Capture directly from the service (bypassing UI/OS filtering)
            var newHotkey = await hotkeyService.CaptureNextKeyAsync();
            
            // Update on UI thread
            Dispatcher.UIThread.Post(() =>
            {
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
                        
                        // Remove error effect after a short delay
                        Task.Delay(2000).ContinueWith(_ =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                IsValid = true;
                                ErrorMessage = string.Empty;
                                UpdateVisualStateClasses();
                            });
                        });
                        
                        IsCapturing = false;
                        UpdateDisplayString();
                        UpdateVisualStateClasses();
                        return;
                    }
                }
                
                // Valid hotkey - update
                IsValid = true;
                ErrorMessage = string.Empty;
                Hotkey = newHotkey;
                HotkeyChanged?.Invoke(this, newHotkey);
                IsCapturing = false;
                UpdateDisplayString();
                UpdateVisualStateClasses();
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsCapturing = false;
                UpdateDisplayString();
                UpdateVisualStateClasses();
                
                Log.Error(ex, "Capture failed");
            });
        }
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
}
