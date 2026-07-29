
namespace CrossMacro.UI.Converters;

public static class ImageMatchModeConverters
{
    public static readonly IValueConverter DisplayText = new FuncValueConverter<EditorImageMatchMode, string>(mode =>
        mode is EditorImageMatchMode.BestMatch
            ? Resources.ResourceManager.GetString("Editor_ImageMatchModeBest", Resources.Culture) ?? "Best match"
            : Resources.ResourceManager.GetString("Editor_ImageMatchModeFirst", Resources.Culture) ?? "First threshold match");
}
