using CommunityToolkit.Mvvm.ComponentModel;
using CrossMacro.UI.Icons;
using CrossMacro.UI.ViewModels;

namespace CrossMacro.UI.Models;

public class NavigationItem : ObservableObject
{
    private string _label = string.Empty;
    private string _localizationKey = string.Empty;

    public required string LocalizationKey
    {
        get => _localizationKey;
        set => SetProperty(ref _localizationKey, value);
    }

    public required string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public required AppIcon Icon { get; set; }
    public required ViewModelBase ViewModel { get; set; }
}
