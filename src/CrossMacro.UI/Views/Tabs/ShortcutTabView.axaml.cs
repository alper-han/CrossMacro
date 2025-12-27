using Avalonia.Controls;
using CrossMacro.UI.ViewModels;

namespace CrossMacro.UI.Views.Tabs;

public partial class ShortcutTabView : UserControl
{
    public ShortcutTabView()
    {
        InitializeComponent();
    }

    private void OnHotkeyChanged(object? sender, string newHotkey)
    {
        if (DataContext is ShortcutViewModel vm)
        {
            vm.OnHotkeyChanged(newHotkey);
        }
    }
}
