using Avalonia.Controls;
using CrossMacro.UI.Services;
using FluentAssertions;

namespace CrossMacro.UI.Tests.Services;

public class ThemeServiceBehaviorTests
{
    [Fact]
    public void TryApplyTheme_ShouldReplaceOnlyActiveThemeDictionary()
    {
        var root = new ResourceDictionary();
        var shared = new ResourceDictionary
        {
            ["Shared.Resource"] = "kept"
        };

        var classic = new ResourceDictionary
        {
            [ThemeCatalog.ThemeMarkerKey] = "Classic",
            ["Theme.Value"] = "classic"
        };
        var nord = new ResourceDictionary
        {
            [ThemeCatalog.ThemeMarkerKey] = "Nord",
            ["Theme.Value"] = "nord"
        };

        root["Theme.Classic"] = classic;
        root["Theme.Nord"] = nord;
        root.MergedDictionaries.Add(shared);
        root.MergedDictionaries.Add(classic);

        var service = new ThemeService(root);

        var result = service.TryApplyTheme("Nord", out var error);

        result.Should().BeTrue();
        error.Should().BeEmpty();
        service.CurrentTheme.Should().Be("Nord");
        root.MergedDictionaries.Should().Contain(shared);
        root.MergedDictionaries.Should().Contain(nord);
        root.MergedDictionaries.Should().NotContain(classic);
        root.MergedDictionaries.Should().HaveCount(2);
    }

    [Fact]
    public void TryApplyTheme_WithUnknownName_ShouldFallbackToClassic()
    {
        var root = new ResourceDictionary();
        var classic = new ResourceDictionary
        {
            [ThemeCatalog.ThemeMarkerKey] = "Classic"
        };
        var dracula = new ResourceDictionary
        {
            [ThemeCatalog.ThemeMarkerKey] = "Dracula"
        };

        root["Theme.Classic"] = classic;
        root["Theme.Dracula"] = dracula;
        root.MergedDictionaries.Add(dracula);

        var service = new ThemeService(root);

        var result = service.TryApplyTheme("UnknownTheme", out var error);

        result.Should().BeFalse();
        error.Should().Contain("Fallback");
        service.CurrentTheme.Should().Be(ThemeCatalog.DefaultThemeName);
        root.MergedDictionaries.Should().Contain(classic);
        root.MergedDictionaries.Should().NotContain(dracula);
    }
}
