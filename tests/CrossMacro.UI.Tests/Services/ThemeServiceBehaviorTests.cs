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
        _ = ThemeCatalog.TryResolve("Nord", out var nordTheme);

        root.MergedDictionaries.Add(shared);
        root.MergedDictionaries.Add(ThemeResourceDictionaryFactory.Create(ThemeCatalog.DefaultTheme));

        var service = new ThemeService(root, new MutableExternalThemeSource());

        var result = service.TryApplyTheme("Nord", out var error);

        _ = result.Should().BeTrue();
        _ = error.Should().BeEmpty();
        _ = service.CurrentTheme.Should().Be("Nord");
        _ = root.MergedDictionaries.Should().Contain(shared);
        _ = root.MergedDictionaries.Should().HaveCount(2);
        _ = ActiveThemeName(root).Should().Be(nordTheme.Name);
    }

    [Fact]
    public void TryApplyTheme_WithUnknownName_ShouldFallbackToDefaultTheme()
    {
        var root = new ResourceDictionary();
        _ = ThemeCatalog.TryResolve("Dracula", out var draculaTheme);
        root.MergedDictionaries.Add(ThemeResourceDictionaryFactory.Create(draculaTheme));

        var service = new ThemeService(root, new MutableExternalThemeSource());

        var result = service.TryApplyTheme("UnknownTheme", out var error);

        _ = result.Should().BeFalse();
        _ = error.Should().Contain("Fallback");
        _ = service.CurrentTheme.Should().Be(ThemeCatalog.DefaultThemeName);
        _ = ActiveThemeName(root).Should().Be(ThemeCatalog.DefaultThemeName);
    }

    [Fact]
    public void TryRefreshThemes_ShouldAddExternalThemesAndAllowApplyingThem()
    {
        var source = new MutableExternalThemeSource();
        var root = new ResourceDictionary();
        root.MergedDictionaries.Add(ThemeResourceDictionaryFactory.Create(ThemeCatalog.DefaultTheme));
        var service = new ThemeService(root, source);

        source.SetThemes([CreateExternalTheme("Aurora")]);

        var refreshResult = service.TryRefreshThemes(out var refreshError);
        var applyResult = service.TryApplyTheme("Aurora", out var applyError);

        _ = refreshResult.Should().BeTrue();
        _ = refreshError.Should().BeEmpty();
        _ = service.AvailableThemes.Should().Contain("Aurora");
        _ = applyResult.Should().BeTrue();
        _ = applyError.Should().BeEmpty();
        _ = service.CurrentTheme.Should().Be("Aurora");
        _ = ActiveThemeName(root).Should().Be("Aurora");
    }

    [Fact]
    public void TryRefreshThemes_WhenCurrentExternalThemeIsRemoved_ShouldFallbackToDefaultTheme()
    {
        var source = new MutableExternalThemeSource();
        source.SetThemes([CreateExternalTheme("Aurora")]);
        var root = new ResourceDictionary();
        root.MergedDictionaries.Add(ThemeResourceDictionaryFactory.Create(ThemeCatalog.DefaultTheme));
        var service = new ThemeService(root, source);
        _ = service.TryApplyTheme("Aurora", out _).Should().BeTrue();

        source.SetThemes([]);

        var result = service.TryRefreshThemes(out var error);

        _ = result.Should().BeFalse();
        _ = error.Should().Contain("no longer available");
        _ = service.CurrentTheme.Should().Be(ThemeCatalog.DefaultThemeName);
        _ = ActiveThemeName(root).Should().Be(ThemeCatalog.DefaultThemeName);
    }

    [Fact]
    public void TryRefreshThemes_WhenExternalSourceThrows_ShouldKeepBuiltInCatalogAndReportFailure()
    {
        var root = new ResourceDictionary();
        var service = new ThemeService(root, new ThrowingExternalThemeSource());

        var result = service.TryRefreshThemes(out var error);

        _ = result.Should().BeFalse();
        _ = error.Should().Contain("External themes could not be loaded");
        _ = service.AvailableThemes.Should().BeEquivalentTo(ThemeCatalog.ThemeNames);
    }

    private static string ActiveThemeName(ResourceDictionary root)
    {
        var activeThemeDictionary = root.MergedDictionaries
            .First(dictionary => dictionary.TryGetResource(ThemeCatalog.ThemeMarkerKey, theme: null, out _));
        _ = activeThemeDictionary.TryGetResource(ThemeCatalog.ThemeMarkerKey, theme: null, out var activeThemeName)
            .Should().BeTrue();
        return activeThemeName.Should().BeOfType<string>().Subject;
    }

    private static ThemeDescriptor CreateExternalTheme(string name)
    {
        return new ThemeDescriptor(name, ThemeCatalog.DefaultTheme.Palette, ThemeSourceKind.ExternalFile, $"/tmp/{name}.json");
    }

    private sealed class ThrowingExternalThemeSource : IExternalThemeSource
    {
        public ExternalThemeLoadResult LoadThemes()
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class MutableExternalThemeSource : IExternalThemeSource
    {
        private IReadOnlyList<ThemeDescriptor> _themes = [];
        private IReadOnlyList<string> _diagnostics = [];

        public void SetThemes(IReadOnlyList<ThemeDescriptor> themes, IReadOnlyList<string>? diagnostics = null)
        {
            _themes = themes;
            _diagnostics = diagnostics ?? [];
        }

        public ExternalThemeLoadResult LoadThemes()
        {
            return new ExternalThemeLoadResult(_themes, _diagnostics);
        }
    }
}
