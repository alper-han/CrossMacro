namespace CrossMacro.UI.Tests.Views.Tabs;

public sealed class SettingsTabViewTests
{
    [Fact]
    public void StartMinimizedSetting_UsesTraySettingsVisibility()
    {
        var xaml = ReadRepoFile("src/CrossMacro.UI/Views/Tabs/SettingsTabView.axaml");
        const string marker = "Text=\"{localization:Loc Settings_StartMinimized}\"";

        var markerIndex = xaml.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Settings_StartMinimized text binding should exist in settings tab XAML.");

        var gridStartIndex = xaml.LastIndexOf("<Grid", markerIndex, StringComparison.Ordinal);
        Assert.True(gridStartIndex >= 0, "Settings_StartMinimized should be inside a Grid.");

        var gridEndIndex = xaml.IndexOf('>', gridStartIndex);
        Assert.True(gridEndIndex > gridStartIndex, "Settings_StartMinimized Grid opening tag should be complete.");

        var gridOpeningTag = xaml[gridStartIndex..(gridEndIndex + 1)];
        Assert.Contains("IsVisible=\"{Binding IsTraySettingsVisible}\"", gridOpeningTag, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CrossMacro.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
