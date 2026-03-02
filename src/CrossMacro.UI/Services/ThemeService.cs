using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace CrossMacro.UI.Services;

public sealed class ThemeService : IThemeService
{
    private readonly IResourceDictionary? _resourceRoot;

    public ThemeService()
    {
    }

    public ThemeService(IResourceDictionary? resourceRoot)
    {
        _resourceRoot = resourceRoot;
    }

    public IReadOnlyList<string> AvailableThemes => ThemeCatalog.ThemeNames;

    public string CurrentTheme { get; private set; } = ThemeCatalog.DefaultThemeName;

    public bool TryApplyTheme(string themeName, out string error)
    {
        error = string.Empty;

        var resourceRoot = _resourceRoot ?? Application.Current?.Resources;
        var mergedDictionaries = resourceRoot?.MergedDictionaries;
        if (resourceRoot == null || mergedDictionaries == null)
        {
            error = "Application resources are not available.";
            CurrentTheme = ThemeCatalog.DefaultThemeName;
            return false;
        }

        var requestedThemeWasValid = ThemeCatalog.TryResolve(themeName, out var requestedTheme);
        var appliedTheme = requestedTheme;
        var appliedFallbackForMissingResource = false;
        if (!TryResolveThemeDictionary(resourceRoot, requestedTheme, out var targetThemeDictionary))
        {
            if (!TryResolveThemeDictionary(resourceRoot, ThemeCatalog.DefaultTheme, out targetThemeDictionary))
            {
                CurrentTheme = ThemeCatalog.DefaultThemeName;
                error = $"Theme resources are missing. Could not apply {ThemeCatalog.DefaultThemeName}.";
                return false;
            }

            appliedTheme = ThemeCatalog.DefaultTheme;
            appliedFallbackForMissingResource = true;
        }

        for (var index = mergedDictionaries.Count - 1; index >= 0; index--)
        {
            if (mergedDictionaries[index].TryGetResource(ThemeCatalog.ThemeMarkerKey, null, out _))
            {
                mergedDictionaries.RemoveAt(index);
            }
        }

        mergedDictionaries.Add(targetThemeDictionary);
        CurrentTheme = appliedTheme.Name;

        if (appliedFallbackForMissingResource)
        {
            error = $"Theme resource not found: {requestedTheme.ResourceKey}. Fallback to {ThemeCatalog.DefaultThemeName} applied.";
            return false;
        }

        if (!requestedThemeWasValid)
        {
            CurrentTheme = ThemeCatalog.DefaultThemeName;
            error = $"Unknown theme '{themeName}'. Fallback to {ThemeCatalog.DefaultThemeName} applied.";
            return false;
        }

        return true;
    }

    private static bool TryResolveThemeDictionary(
        IResourceDictionary root,
        ThemeDescriptor descriptor,
        out IResourceDictionary dictionary)
    {
        dictionary = null!;
        if (!root.TryGetResource(descriptor.ResourceKey, null, out var resource) || resource is not IResourceDictionary resolved)
        {
            return false;
        }

        dictionary = resolved;
        return true;
    }
}
