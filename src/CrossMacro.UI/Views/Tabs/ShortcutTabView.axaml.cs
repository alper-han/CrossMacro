namespace CrossMacro.UI.Views.Tabs;

public partial class ShortcutTabView : UserControl
{
    private HotkeyCapture? _hotkeyCapture;

    public ShortcutTabView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void OnHotkeyChanged(object? sender, string newHotkey)
    {
        if (sender is HotkeyCapture && DataContext is ShortcutViewModel vm)
        {
            vm.OnHotkeyChanged(newHotkey);
        }
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _hotkeyCapture = this.FindControl<HotkeyCapture>("HotkeyInput");
        if (_hotkeyCapture is not null && DataContext is ShortcutViewModel vm)
        {
            _hotkeyCapture.ValidationFunc = vm.ValidateShortcutHotkey;
        }
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_hotkeyCapture is { } capture)
        {
            capture.ValidationFunc = null;
        }
        _hotkeyCapture = null;
    }
}
