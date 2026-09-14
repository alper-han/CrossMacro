
namespace CrossMacro.UI.Converters;

public static class EditorScreenTargetColorSourceDisplayConverters
{
    public static string FormatSource(EditorActionScreenTargetColorSource source, ILocalizationService? localizationService = null)
    {
        return source switch
        {
            EditorActionScreenTargetColorSource.Variable => Localize("Editor_TargetColorSourceVariable", "Variable", localizationService),
            EditorActionScreenTargetColorSource.ManualHex => Localize("Editor_TargetColorSourceManualHex", "Manual hex", localizationService),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, message: null),
        };
    }

    private static string Localize(string key, string fallback, ILocalizationService? localizationService)
    {
        var localized = localizationService?[key];
        return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
    }
}
