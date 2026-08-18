using System.Collections.ObjectModel;

namespace CrossMacro.UI.Themes;

public static class ThemeResourceDictionaryFactory
{
    private static readonly IReadOnlyList<(string BrushKey, string ColorKey)> BrushMappings =
        new ReadOnlyCollection<(string BrushKey, string ColorKey)>(
        [
            ("SliderThumbBackground", nameof(ThemePalette.PrimaryColor)),
            ("SliderThumbBackgroundPointerOver", nameof(ThemePalette.PrimaryHoverColor)),
            ("SliderThumbBackgroundPressed", nameof(ThemePalette.PrimaryPressedColor)),
            ("SliderThumbBackgroundDisabled", nameof(ThemePalette.TextSecondaryColor)),
            ("SliderTrackFill", nameof(ThemePalette.SurfaceHoverColor)),
            ("SliderTrackFillPointerOver", nameof(ThemePalette.SurfaceHoverColor)),
            ("SliderTrackFillPressed", nameof(ThemePalette.SurfaceColor)),
            ("SliderTrackFillDisabled", nameof(ThemePalette.SurfaceHoverColor)),
            ("SliderTrackValueFill", nameof(ThemePalette.PrimaryColor)),
            ("SliderTrackValueFillPointerOver", nameof(ThemePalette.PrimaryHoverColor)),
            ("SliderTrackValueFillPressed", nameof(ThemePalette.PrimaryPressedColor)),
            ("SliderTrackValueFillDisabled", nameof(ThemePalette.TextSecondaryColor)),
            ("SliderTickBarFill", nameof(ThemePalette.SurfaceHoverColor)),
            ("SliderTickBarFillDisabled", nameof(ThemePalette.TextSecondaryColor)),
            ("PrimaryBrush", nameof(ThemePalette.PrimaryColor)),
            ("PrimaryHoverBrush", nameof(ThemePalette.PrimaryHoverColor)),
            ("PrimaryPressedBrush", nameof(ThemePalette.PrimaryPressedColor)),
            ("SuccessBrush", nameof(ThemePalette.SuccessColor)),
            ("SuccessHoverBrush", nameof(ThemePalette.SuccessHoverColor)),
            ("SuccessPressedBrush", nameof(ThemePalette.SuccessPressedColor)),
            ("DangerBrush", nameof(ThemePalette.DangerColor)),
            ("DangerHoverBrush", nameof(ThemePalette.DangerHoverColor)),
            ("DangerPressedBrush", nameof(ThemePalette.DangerPressedColor)),
            ("BackgroundBrush", nameof(ThemePalette.BackgroundColor)),
            ("SurfaceBrush", nameof(ThemePalette.SurfaceColor)),
            ("SurfaceHoverBrush", nameof(ThemePalette.SurfaceHoverColor)),
            ("TextPrimaryBrush", nameof(ThemePalette.TextPrimaryColor)),
            ("TextSecondaryBrush", nameof(ThemePalette.TextSecondaryColor)),
            ("WarningBrush", nameof(ThemePalette.WarningColor)),
            ("WarningHoverBrush", nameof(ThemePalette.WarningHoverColor)),
            ("AccentBrush", nameof(ThemePalette.AccentColor)),
            ("TextOnPrimaryBrush", nameof(ThemePalette.TextOnPrimaryColor)),
            ("TextOnSuccessBrush", nameof(ThemePalette.TextOnSuccessColor)),
            ("TextOnDangerBrush", nameof(ThemePalette.TextOnDangerColor)),
            ("TextOnWarningBrush", nameof(ThemePalette.TextOnWarningColor)),
        ]);

    public static IReadOnlyList<string> ResourceKeys { get; } = BuildResourceKeys();

    public static ResourceDictionary Create(ThemeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var colors = descriptor.Palette.ParseColors();
        var dictionary = new ResourceDictionary
        {
            [ThemeCatalog.ThemeMarkerKey] = descriptor.Name,
        };

        foreach (var (key, color) in colors)
        {
            dictionary[key] = color;
        }

        foreach (var (brushKey, colorKey) in BrushMappings)
        {
            dictionary[brushKey] = new SolidColorBrush(colors[colorKey]);
        }

        return dictionary;
    }

    public static void ReplaceActiveTheme(IResourceDictionary resourceRoot, IResourceDictionary themeDictionary)
    {
        ArgumentNullException.ThrowIfNull(resourceRoot);
        ArgumentNullException.ThrowIfNull(themeDictionary);

        var mergedDictionaries = resourceRoot.MergedDictionaries;
        for (var index = mergedDictionaries.Count - 1; index >= 0; index--)
        {
            if (mergedDictionaries[index].TryGetResource(ThemeCatalog.ThemeMarkerKey, theme: null, out _))
            {
                mergedDictionaries.RemoveAt(index);
            }
        }

        mergedDictionaries.Add(themeDictionary);
    }

    private static IReadOnlyList<string> BuildResourceKeys()
    {
        var keys = new List<string>(1 + ThemePalette.ColorKeys.Count + BrushMappings.Count)
        {
            ThemeCatalog.ThemeMarkerKey,
        };

        keys.AddRange(ThemePalette.ColorKeys);
        keys.AddRange(BrushMappings.Select(mapping => mapping.BrushKey));
        return new ReadOnlyCollection<string>(keys);
    }
}
