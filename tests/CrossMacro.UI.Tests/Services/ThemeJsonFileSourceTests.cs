namespace CrossMacro.UI.Tests.Services;

public sealed class ThemeJsonFileSourceTests : IDisposable
{
    private readonly string _themeDirectory = Path.Combine(Path.GetTempPath(), $"crossmacro-themes-{Guid.NewGuid():N}");

    [Fact]
    public void LoadThemes_WithValidJsonFile_ShouldReturnThemeDescriptor()
    {
        _ = Directory.CreateDirectory(_themeDirectory);
        var filePath = Path.Combine(_themeDirectory, "aurora.json");
        File.WriteAllText(filePath, BuildThemeJson("Aurora"));

        var source = new ThemeJsonFileSource(new StaticThemeDirectoryResolver(_themeDirectory));

        var result = source.LoadThemes();

        _ = result.Diagnostics.Should().BeEmpty();
        _ = result.Themes.Should().ContainSingle(theme => theme.Name == "Aurora");
    }

    [Fact]
    public void LoadThemes_InfersAppearanceWhenMetadataIsOmitted()
    {
        _ = Directory.CreateDirectory(_themeDirectory);
        File.WriteAllText(Path.Combine(_themeDirectory, "light.json"), BuildThemeJson("Light Custom", background: "#ffffff", foreground: "#111111"));

        var result = new ThemeJsonFileSource(new StaticThemeDirectoryResolver(_themeDirectory)).LoadThemes();

        _ = result.Themes.Should().ContainSingle().Which.Appearance.Should().Be(ThemeAppearance.Light);
    }

    [Fact]
    public void LoadThemes_UsesExplicitAppearanceMetadata()
    {
        _ = Directory.CreateDirectory(_themeDirectory);
        var json = BuildThemeJson("Explicit Light", background: "#111111", foreground: "#ffffff", appearance: "light");
        File.WriteAllText(Path.Combine(_themeDirectory, "explicit.json"), json);

        var result = new ThemeJsonFileSource(new StaticThemeDirectoryResolver(_themeDirectory)).LoadThemes();

        _ = result.Themes.Should().ContainSingle().Which.Appearance.Should().Be(ThemeAppearance.Light);
    }

    [Fact]
    public void LoadThemes_RejectsInvalidAppearanceMetadata()
    {
        _ = Directory.CreateDirectory(_themeDirectory);
        File.WriteAllText(Path.Combine(_themeDirectory, "invalid.json"), BuildThemeJson("Invalid", appearance: "sepia"));

        var result = new ThemeJsonFileSource(new StaticThemeDirectoryResolver(_themeDirectory)).LoadThemes();

        _ = result.Themes.Should().BeEmpty();
        _ = result.Diagnostics.Should().ContainSingle().Which.Should().Contain("appearance");
    }

    [Fact]
    public void LoadThemes_InfersDarkAppearanceFromDarkBackgroundEvenWhenTextIsDarker()
    {
        _ = Directory.CreateDirectory(_themeDirectory);
        File.WriteAllText(Path.Combine(_themeDirectory, "dark.json"), BuildThemeJson("Dark Custom", background: "#202020", foreground: "#101010"));

        var result = new ThemeJsonFileSource(new StaticThemeDirectoryResolver(_themeDirectory)).LoadThemes();

        _ = result.Themes.Should().ContainSingle().Which.Appearance.Should().Be(ThemeAppearance.Dark);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public void LoadThemes_RejectsNumericAppearanceMetadata(string appearance)
    {
        _ = Directory.CreateDirectory(_themeDirectory);
        File.WriteAllText(Path.Combine(_themeDirectory, "numeric.json"), BuildThemeJson("Numeric", appearance: appearance));

        var result = new ThemeJsonFileSource(new StaticThemeDirectoryResolver(_themeDirectory)).LoadThemes();

        _ = result.Themes.Should().BeEmpty();
        _ = result.Diagnostics.Should().ContainSingle().Which.Should().Contain("appearance");
    }
    [Fact]
    public void LoadThemes_WithInvalidJsonFile_ShouldSkipThemeAndReportDiagnostic()
    {
        _ = Directory.CreateDirectory(_themeDirectory);
        var filePath = Path.Combine(_themeDirectory, "broken.json");
        File.WriteAllText(filePath, "{\"name\":\"Broken\",\"palette\":{\"primaryColor\":\"not-a-color\"}}");

        var source = new ThemeJsonFileSource(new StaticThemeDirectoryResolver(_themeDirectory));

        var result = source.LoadThemes();

        _ = result.Themes.Should().BeEmpty();
        _ = result.Diagnostics.Should().ContainSingle();
        _ = result.Diagnostics[0].Should().Contain("broken.json");
    }

    [Fact]
    public void LoadThemes_WhenThemeDirectoryCannotBeCreated_ShouldReturnDiagnosticInsteadOfThrowing()
    {
        var blockingFile = Path.Combine(_themeDirectory, "not-a-directory");
        _ = Directory.CreateDirectory(_themeDirectory);
        File.WriteAllText(blockingFile, "blocked");

        var source = new ThemeJsonFileSource(new StaticThemeDirectoryResolver(blockingFile));

        var result = source.LoadThemes();

        _ = result.Themes.Should().BeEmpty();
        _ = result.Diagnostics.Should().ContainSingle();
        _ = result.Diagnostics[0].Should().Contain("unavailable");
    }

    [Fact]
    public void LoadThemes_SkipsUnderscorePrefixedFiles()
    {
        _ = Directory.CreateDirectory(_themeDirectory);
        File.WriteAllText(Path.Combine(_themeDirectory, "_draft.json"), BuildThemeJson("Draft"));
        File.WriteAllText(Path.Combine(_themeDirectory, "aurora.json"), BuildThemeJson("Aurora"));

        var source = new ThemeJsonFileSource(new StaticThemeDirectoryResolver(_themeDirectory));

        var result = source.LoadThemes();

        _ = result.Themes.Should().ContainSingle(theme => theme.Name == "Aurora");
        _ = result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void LoadThemes_ProvisionsTemplateAndReadmeIntoThemeDirectory()
    {
        _ = Directory.CreateDirectory(_themeDirectory);

        var source = new ThemeJsonFileSource(new StaticThemeDirectoryResolver(_themeDirectory), new ThemeSampleProvisioner());

        _ = source.LoadThemes();

        _ = File.Exists(Path.Combine(_themeDirectory, "_template.json")).Should().BeTrue();
        _ = File.Exists(Path.Combine(_themeDirectory, "README.md")).Should().BeTrue();
        // The provisioned template must not leak into the theme list.
        _ = source.LoadThemes().Themes.Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_themeDirectory))
        {
            Directory.Delete(_themeDirectory, recursive: true);
        }
    }

    private static string BuildThemeJson(
        string name,
        string? background = null,
        string? foreground = null,
        string? appearance = null)
    {
        var palette = ThemeCatalog.DefaultTheme.Palette.EnumerateColorValues()
            .ToDictionary(
                entry => char.ToLowerInvariant(entry.Key[0]) + entry.Key[1..],
                entry => entry.Value,
                StringComparer.Ordinal);
        if (background is not null) { palette["backgroundColor"] = background; }
        if (foreground is not null) { palette["textPrimaryColor"] = foreground; }

        var document = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = name,
            ["palette"] = palette,
        };
        if (appearance is not null) { document["appearance"] = appearance; }
        return JsonSerializer.Serialize(document);
    }

    private sealed class StaticThemeDirectoryResolver(string themeDirectory) : IThemeDirectoryResolver
    {
        public string GetThemeDirectoryPath()
        {
            return themeDirectory;
        }
    }
}
