
namespace CrossMacro.UI.Views.Tabs;

public static class EditorScreenTargetColorSourceDisplayConverters
{
    private static ILocalizationService? _localizationService;

    public static void Configure(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public static string FormatSource(EditorActionScreenTargetColorSource source)
    {
        return source switch
        {
            EditorActionScreenTargetColorSource.Variable => Localize("Editor_TargetColorSourceVariable", "Variable"),
            _ => Localize("Editor_TargetColorSourceManualHex", "Manual hex"),
        };
    }

    private static string Localize(string key, string fallback)
    {
        var localized = _localizationService?[key];
        return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
    }
}
