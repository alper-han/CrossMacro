using System.Collections.ObjectModel;

namespace CrossMacro.UI.Themes;

/// <summary>
/// Registry of the themes shipped with the application. Each built-in theme is an
/// embedded JSON file under Themes/, loaded through the same pipeline as external
/// user themes (see <see cref="ThemeJsonFileSource"/>).
/// </summary>
public static class ThemeCatalog
{
    public const string DefaultThemeName = "Mocha";
    public const string ThemeMarkerKey = "Theme.Name";

    public static IReadOnlyList<ThemeDescriptor> Themes { get; } = BuiltInThemeLoader.LoadAll();

    public static IReadOnlyList<string> ThemeNames { get; } =
        new ReadOnlyCollection<string>(Themes.Select(theme => theme.Name).ToArray());

    public static ThemeDescriptor DefaultTheme { get; } = Themes
        .First(theme => string.Equals(theme.Name, DefaultThemeName, StringComparison.Ordinal));

    public static bool TryResolve(string? name, out ThemeDescriptor descriptor)
    {
        descriptor = Themes.FirstOrDefault(theme =>
            string.Equals(theme.Name, name, StringComparison.OrdinalIgnoreCase)) ?? DefaultTheme;
        return string.Equals(descriptor.Name, name, StringComparison.OrdinalIgnoreCase);
    }
}
