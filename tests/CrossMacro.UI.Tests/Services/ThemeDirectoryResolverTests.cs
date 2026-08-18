namespace CrossMacro.UI.Tests.Services;

public sealed class ThemeDirectoryResolverTests
{
    [Fact]
    public void GetThemeDirectoryPath_ShouldTrackApplicationConfigRoot()
    {
        var resolver = new ThemeDirectoryResolver();

        var result = resolver.GetThemeDirectoryPath();

        _ = result.Should().Be(Path.Combine(PathHelper.GetConfigDirectory(), "themes"));
    }
}
