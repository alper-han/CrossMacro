
namespace CrossMacro.UI.Models;

public class NavigationItem : ObservableObject
{
    public required string LocalizationKey
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public required string Label
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public required AppIcon Icon { get; set; }
    public required ViewModelBase ViewModel { get; set; }
}
