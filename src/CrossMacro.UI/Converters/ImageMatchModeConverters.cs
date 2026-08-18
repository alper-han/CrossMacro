
namespace CrossMacro.UI.Converters;

public static class ImageMatchModeConverters
{
    public static readonly IValueConverter DisplayText = new FuncValueConverter<EditorImageMatchMode, string>(mode =>
        mode switch
        {
            EditorImageMatchMode.Automatic => Resources.ResourceManager.GetString("Editor_ImageMatchModeAutomatic", Resources.Culture) ?? "Automatic (recommended)",
            EditorImageMatchMode.BestMatch => Resources.ResourceManager.GetString("Editor_ImageMatchModeBest", Resources.Culture) ?? "Best match",
            EditorImageMatchMode.FirstThresholdMatch => Resources.ResourceManager.GetString("Editor_ImageMatchModeFirst", Resources.Culture) ?? "First threshold match",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Image match mode is invalid."),
        });
}
