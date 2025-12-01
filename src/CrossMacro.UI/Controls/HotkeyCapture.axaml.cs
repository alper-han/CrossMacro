using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CrossMacro.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.UI.Controls;

public partial class HotkeyCapture : UserControl
{
    public static readonly StyledProperty<string> HotkeyProperty =
        AvaloniaProperty.Register<HotkeyCapture, string>(nameof(Hotkey), "F8");

    public static readonly DirectProperty<HotkeyCapture, bool> IsCapturingProperty =
        AvaloniaProperty.RegisterDirect<HotkeyCapture, bool>(
            nameof(IsCapturing),
            o => o.IsCapturing);

    private bool _isCapturing;

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

    public event EventHandler<string>? HotkeyChanged;

    public static readonly DirectProperty<HotkeyCapture, string> DisplayStringProperty =
        AvaloniaProperty.RegisterDirect<HotkeyCapture, string>(
            nameof(DisplayString),
            o => o.DisplayString);

    private string _displayString = "F8";

    public string DisplayString
    {
        get => _displayString;
        private set => SetAndRaise(DisplayStringProperty, ref _displayString, value);
    }

    public HotkeyCapture()
    {
        InitializeComponent();
        UpdateDisplayString();
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

        try
        {
            // Capture directly from the service (bypassing UI/OS filtering)
            var newHotkey = await hotkeyService.CaptureNextKeyAsync();
            
            // Update on UI thread
            Dispatcher.UIThread.Post(() =>
            {
                Hotkey = newHotkey;
                HotkeyChanged?.Invoke(this, newHotkey);
                IsCapturing = false;
                UpdateDisplayString();
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsCapturing = false;
                UpdateDisplayString();
                Console.WriteLine($"Capture failed: {ex}");
            });
        }
    }
}
