using CommunityToolkit.Mvvm.ComponentModel;

namespace CrossMacro.UI.Localization;

public sealed partial class LanguageOption : ObservableObject
{
    public required string Code { get; init; }

    [ObservableProperty]
    private string _displayName = string.Empty;
}
