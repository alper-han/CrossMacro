
namespace CrossMacro.UI.Tests.Services;

public sealed class ThemeServiceBehaviorTests
{
    [Fact]
    public void TryApplyTheme_ShouldReplaceOnlyActiveThemeDictionary()
    {
        var root = new ResourceDictionary();
        var shared = new ResourceDictionary
        {
            ["Shared.Resource"] = "kept",
        };

        var classic = new ResourceDictionary
        {
            [ThemeCatalog.ThemeMarkerKey] = "Classic",
            ["Theme.Value"] = "classic",
        };
        var nord = new ResourceDictionary
        {
            [ThemeCatalog.ThemeMarkerKey] = "Nord",
            ["Theme.Value"] = "nord",
        };

        root["Theme.Classic"] = classic;
        root["Theme.Nord"] = nord;
        root.MergedDictionaries.Add(shared);
        root.MergedDictionaries.Add(classic);

        var service = new ThemeService(root);

        var result = service.TryApplyTheme("Nord", out var error);

        _ = result.Should().BeTrue();
        _ = error.Should().BeEmpty();
        _ = service.CurrentTheme.Should().Be("Nord");
        _ = root.MergedDictionaries.Should().Contain(shared);
        _ = root.MergedDictionaries.Should().Contain(nord);
        _ = root.MergedDictionaries.Should().NotContain(classic);
        _ = root.MergedDictionaries.Should().HaveCount(2);
    }

    [Fact]
    public void TryApplyTheme_WithUnknownName_ShouldFallbackToDefaultTheme()
    {
        var root = new ResourceDictionary();
        var fallbackTheme = new ResourceDictionary
        {
            [ThemeCatalog.ThemeMarkerKey] = ThemeCatalog.DefaultThemeName,
        };
        var dracula = new ResourceDictionary
        {
            [ThemeCatalog.ThemeMarkerKey] = "Dracula",
        };

        root[ThemeCatalog.DefaultTheme.ResourceKey] = fallbackTheme;
        root["Theme.Dracula"] = dracula;
        root.MergedDictionaries.Add(dracula);

        var service = new ThemeService(root);

        var result = service.TryApplyTheme("UnknownTheme", out var error);

        _ = result.Should().BeFalse();
        _ = error.Should().Contain("Fallback");
        _ = service.CurrentTheme.Should().Be(ThemeCatalog.DefaultThemeName);
        _ = root.MergedDictionaries.Should().Contain(fallbackTheme);
        _ = root.MergedDictionaries.Should().NotContain(dracula);
    }

    [Fact]
    public void TryApplyTheme_WithMissingRequestedResource_ShouldApplyDefaultFallbackDictionary()
    {
        var root = new ResourceDictionary();
        var fallbackTheme = new ResourceDictionary
        {
            [ThemeCatalog.ThemeMarkerKey] = ThemeCatalog.DefaultThemeName,
        };
        var dracula = new ResourceDictionary
        {
            [ThemeCatalog.ThemeMarkerKey] = "Dracula",
        };

        // "Nord" exists in ThemeCatalog but is intentionally missing from runtime resources.
        root[ThemeCatalog.DefaultTheme.ResourceKey] = fallbackTheme;
        root.MergedDictionaries.Add(dracula);

        var service = new ThemeService(root);

        var result = service.TryApplyTheme("Nord", out var error);

        _ = result.Should().BeFalse();
        _ = error.Should().Contain("Theme resource not found");
        _ = service.CurrentTheme.Should().Be(ThemeCatalog.DefaultThemeName);
        _ = root.MergedDictionaries.Should().Contain(fallbackTheme);
        _ = root.MergedDictionaries.Should().NotContain(dracula);
    }
}
