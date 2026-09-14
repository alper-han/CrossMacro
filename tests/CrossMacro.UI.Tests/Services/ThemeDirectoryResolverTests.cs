namespace CrossMacro.UI.Tests.Services;

public sealed class ThemeDirectoryResolverTests
{
    [Fact]
    public void GetThemeDirectoryPath_ShouldTrackApplicationConfigRoot()
    {
        var paths = new ApplicationPaths(Path.Combine(Path.GetTempPath(), "theme-test-root"));
        var resolver = new ThemeDirectoryResolver(paths);

        var result = resolver.GetThemeDirectoryPath();

        _ = result.Should().Be(Path.Combine(paths.ConfigDirectory, "themes"));
    }
}
