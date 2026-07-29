
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

}
