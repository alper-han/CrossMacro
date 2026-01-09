using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        
        // Add hover effects manually
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
    }
    
    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        ApplyHoverEffect();
    }
    
    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        // Don't remove hover effect if we're capturing a key
        if (!IsCapturing)
        {
            RemoveHoverEffect();
        }
    }
    
    private void ApplyHoverEffect()
    {
        var border = this.FindControl<Border>("HotkeyBorder");
        var icon = this.FindControl<Path>("EditIcon");
        
        if (border != null)
        {
            if (Application.Current != null && Application.Current.Resources.TryGetResource("SurfaceBrush", null, out var bgBrush))
                border.Background = bgBrush as Avalonia.Media.IBrush;
                
            if (Application.Current != null && Application.Current.Resources.TryGetResource("AccentBrush", null, out var borderBrush))
                border.BorderBrush = borderBrush as Avalonia.Media.IBrush;
        }
        
        if (icon != null)
        {
            icon.Opacity = 1.0;
        }
    }
    
    private void RemoveHoverEffect()
    {
        var border = this.FindControl<Border>("HotkeyBorder");
        var icon = this.FindControl<Path>("EditIcon");
        
        if (border != null)
        {
            if (Application.Current != null && Application.Current.Resources.TryGetResource("SurfaceHoverBrush", null, out var bgBrush))
                border.Background = bgBrush as Avalonia.Media.IBrush;
                
            border.BorderBrush = Avalonia.Media.Brushes.Transparent;
            border.BoxShadow = default;
        }
        
        if (icon != null)
        {
            icon.Opacity = 0.5;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HotkeyProperty)
        {
            UpdateDisplayString();
        }
    }

    private void UpdateDisplayString()
    {
        DisplayString = IsCapturing ? "Press a key..." : Hotkey;
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
        ApplyHoverEffect(); // Keep hover effect during capture

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
                        ApplyErrorEffect();
                        
                        // Remove error effect after a short delay
                        Task.Delay(2000).ContinueWith(_ =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                IsValid = true;
                                ErrorMessage = string.Empty;
                                RemoveErrorEffect();
                                
                                if (!IsPointerOver)
                                {
                                    RemoveHoverEffect();
                                }
                            });
                        });
                        
                        IsCapturing = false;
                        UpdateDisplayString();
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
                
                // Check if pointer is still over the control
                if (!IsPointerOver)
                {
                    RemoveHoverEffect();
                }
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsCapturing = false;
                UpdateDisplayString();
                
                // Check if pointer is still over the control
                if (!IsPointerOver)
                {
                    RemoveHoverEffect();
                }
                
                Log.Error(ex, "Capture failed");
            });
        }
    }
    
    private void ApplyErrorEffect()
    {
        var border = this.FindControl<Border>("HotkeyBorder");
        
        if (border != null)
        {
            if (Application.Current != null && Application.Current.Resources.TryGetResource("DangerBrush", null, out var borderBrush))
                border.BorderBrush = borderBrush as Avalonia.Media.IBrush;
                
            border.BorderThickness = new Thickness(2);
        }
    }
    
    private void RemoveErrorEffect()
    {
        var border = this.FindControl<Border>("HotkeyBorder");
        
        if (border != null)
        {
            border.BorderBrush = Avalonia.Media.Brushes.Transparent;
            border.BorderThickness = new Thickness(1);
        }
    }
}
