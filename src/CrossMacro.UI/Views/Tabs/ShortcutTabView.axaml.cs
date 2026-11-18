namespace CrossMacro.UI.Views.Tabs;

public partial class ShortcutTabView : UserControl
{
    public ShortcutTabView()
    {
        InitializeComponent();
    }

    public void OnHotkeyChanged(object? sender, string newHotkey)
    {
        if (sender is HotkeyCapture && DataContext is ShortcutViewModel vm)
        {
            vm.OnHotkeyChanged(newHotkey);
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (this.FindControl<HotkeyCapture>("HotkeyInput") is { } capture)
        {
            capture.ValidationFunc = DataContext is ShortcutViewModel vm ? vm.ValidateShortcutHotkey : null;
        }
    }
}
