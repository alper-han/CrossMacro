using Avalonia.Media;
using CrossMacro.UI.ViewModels;

namespace CrossMacro.UI.Models;

public class NavigationItem
{
    public string Label { get; set; }
    public string Icon { get; set; } // Could be a geometry string or emoji/character
    public ViewModelBase ViewModel { get; set; }
    public bool IsSelected { get; set; }
}
