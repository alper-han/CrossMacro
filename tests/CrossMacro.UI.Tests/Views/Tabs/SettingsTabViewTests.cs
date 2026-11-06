namespace CrossMacro.UI.Tests.Views.Tabs;

public sealed class SettingsTabViewTests
{
    [Theory]
    [InlineData("Settings_EnableTrayIcon")]
    [InlineData("Settings_StartMinimized")]
    [InlineData("Settings_HideToTrayOnRecording")]
    [InlineData("Settings_HideToTrayOnPlayback")]
    public void TraySettings_AreNotHiddenByRuntime(string setting)
    {
        var xaml = ReadRepoFile("src/CrossMacro.UI/Views/Tabs/SettingsTabView.axaml");
        var marker = $"Text=\"{{localization:Loc {setting}}}\"";

        var markerIndex = xaml.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"{setting} text binding should exist in settings tab XAML.");

        var gridStartIndex = xaml.LastIndexOf("<Grid", markerIndex, StringComparison.Ordinal);
        Assert.True(gridStartIndex >= 0, $"{setting} should be inside a Grid.");

        var gridEndIndex = xaml.IndexOf('>', gridStartIndex);
        Assert.True(gridEndIndex > gridStartIndex, $"{setting} Grid opening tag should be complete.");

        var gridOpeningTag = xaml[gridStartIndex..(gridEndIndex + 1)];
        Assert.DoesNotContain("IsVisible=", gridOpeningTag, StringComparison.Ordinal);
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
