using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CrossMacro.UI.Services;

public sealed record ThemeDescriptor(string Name, string ResourceKey, string SourcePath);

public static class ThemeCatalog
{
    public const string DefaultThemeName = "Mocha";
    public const string ThemeMarkerKey = "Theme.Name";

    private static readonly IReadOnlyList<ThemeDescriptor> ThemeDescriptors = new ReadOnlyCollection<ThemeDescriptor>(
        new[]
        {
            new ThemeDescriptor("Classic", "Theme.Classic", "/Themes/Classic.axaml"),
            new ThemeDescriptor("Latte", "Theme.Latte", "/Themes/Latte.axaml"),
            new ThemeDescriptor("Mocha", "Theme.Mocha", "/Themes/Mocha.axaml"),
            new ThemeDescriptor("Dracula", "Theme.Dracula", "/Themes/Dracula.axaml"),
            new ThemeDescriptor("Nord", "Theme.Nord", "/Themes/Nord.axaml"),
            new ThemeDescriptor("Everforest", "Theme.Everforest", "/Themes/Everforest.axaml"),
            new ThemeDescriptor("Gruvbox", "Theme.Gruvbox", "/Themes/Gruvbox.axaml"),
            new ThemeDescriptor("Solarized", "Theme.Solarized", "/Themes/Solarized.axaml"),
            new ThemeDescriptor("Crimson", "Theme.Crimson", "/Themes/Crimson.axaml")
        });

    public static IReadOnlyList<ThemeDescriptor> Themes => ThemeDescriptors;

    public static IReadOnlyList<string> ThemeNames { get; } =
        new ReadOnlyCollection<string>(ThemeDescriptors.Select(theme => theme.Name).ToArray());

    public static ThemeDescriptor DefaultTheme { get; } = ThemeDescriptors
        .First(theme => string.Equals(theme.Name, DefaultThemeName, StringComparison.Ordinal));

    public static bool TryResolve(string? name, out ThemeDescriptor descriptor)
    {
        descriptor = ThemeDescriptors.FirstOrDefault(theme =>
            string.Equals(theme.Name, name, StringComparison.OrdinalIgnoreCase)) ?? DefaultTheme;
        return string.Equals(descriptor.Name, name, StringComparison.OrdinalIgnoreCase);
    }
}
