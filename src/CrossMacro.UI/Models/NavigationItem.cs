using Avalonia.Media;
using CrossMacro.UI.ViewModels;

namespace CrossMacro.UI.Models;

public class NavigationItem
{
    public required string Label { get; set; }
    public required string Icon { get; set; } // Could be a geometry string or emoji/character
    public required ViewModelBase ViewModel { get; set; }
    public bool IsSelected { get; set; }
}
