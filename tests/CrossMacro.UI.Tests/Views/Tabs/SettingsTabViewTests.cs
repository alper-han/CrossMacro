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

    [Fact]
    public void ProfileManagement_AppearsImmediatelyBeforeSupportAndKeepsProfileActionsTogether()
    {
        var xaml = ReadRepoFile("src/CrossMacro.UI/Views/Tabs/SettingsTabView.axaml");
        const string profileMarker = "<!-- Profile Management -->";
        const string supportMarker = "<!-- Support Section -->";
        const string switchCommand = "Command=\"{Binding SwitchProfile}\"";
        const string deleteCommand = "Command=\"{Binding DeleteSelectedProfile}\"";
        const string createCommand = "Command=\"{Binding CreateProfile}\"";
        const string renameCommand = "Command=\"{Binding RenameSelectedProfile}\"";

        var profileIndex = xaml.IndexOf(profileMarker, StringComparison.Ordinal);
        Assert.True(profileIndex >= 0, "Profile Management section should exist in settings tab XAML.");

        var supportIndex = xaml.IndexOf(supportMarker, StringComparison.Ordinal);
        Assert.True(supportIndex >= 0, "Support section should exist in settings tab XAML.");
        Assert.True(profileIndex < supportIndex, "Profile Management should appear before Support.");

        var previousSectionIndex = xaml.LastIndexOf("<!--", supportIndex - 1, StringComparison.Ordinal);
        Assert.Equal(profileIndex, previousSectionIndex);

        var switchIndex = xaml.IndexOf(switchCommand, profileIndex, StringComparison.Ordinal);
        Assert.True(switchIndex >= 0, "SwitchProfile command should exist in Profile Management.");

        var profileSelectorGridStartIndex = xaml.LastIndexOf("<Grid", switchIndex, StringComparison.Ordinal);
        Assert.True(profileSelectorGridStartIndex >= profileIndex, "SwitchProfile should be inside the profile selector Grid.");

        var profileSelectorGridEndIndex = xaml.IndexOf("</Grid>", switchIndex, StringComparison.Ordinal);
        Assert.True(profileSelectorGridEndIndex > switchIndex, "Profile selector Grid should close after SwitchProfile.");

        var profileSelectorGrid = xaml[profileSelectorGridStartIndex..(profileSelectorGridEndIndex + "</Grid>".Length)];
        Assert.Contains("ItemsSource=\"{Binding AvailableProfiles}\"", profileSelectorGrid, StringComparison.Ordinal);
        Assert.Contains(switchCommand, profileSelectorGrid, StringComparison.Ordinal);
        Assert.Contains(deleteCommand, profileSelectorGrid, StringComparison.Ordinal);

        var profileSection = xaml[profileIndex..supportIndex];
        Assert.Equal(2, CountOccurrences(profileSection, "ColumnDefinitions=\"*,Auto,Auto\" ColumnSpacing=\"12\""));
        Assert.Contains("Text=\"{localization:Loc Settings_Profiles}\"", profileSection, StringComparison.Ordinal);
        Assert.Contains("Text=\"{localization:Loc Settings_ProfileSwitch}\"", profileSection, StringComparison.Ordinal);
        Assert.Contains("Text=\"{localization:Loc Settings_ProfileDelete}\"", profileSection, StringComparison.Ordinal);
        Assert.Contains("PlaceholderText=\"{localization:Loc Settings_ProfileNamePlaceholder}\"", profileSection, StringComparison.Ordinal);
        Assert.Contains("Text=\"{localization:Loc Settings_ProfileNew}\"", profileSection, StringComparison.Ordinal);
        Assert.Contains("Text=\"{localization:Loc Settings_ProfileRename}\"", profileSection, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Profiles\"", profileSection, StringComparison.Ordinal);
        Assert.DoesNotContain("PlaceholderText=\"Profile name\"", profileSection, StringComparison.Ordinal);
        AssertProfileActionButtonStretches(profileSection, switchCommand);
        AssertProfileActionButtonStretches(profileSection, deleteCommand);
        AssertProfileActionButtonStretches(profileSection, createCommand);
        AssertProfileActionButtonStretches(profileSection, renameCommand);
    }

    private static void AssertProfileActionButtonStretches(string profileSection, string command)
    {
        var commandIndex = profileSection.IndexOf(command, StringComparison.Ordinal);
        Assert.True(commandIndex >= 0, $"{command} should exist in Profile Management.");

        var buttonStartIndex = profileSection.LastIndexOf("<Button", commandIndex, StringComparison.Ordinal);
        Assert.True(buttonStartIndex >= 0, $"{command} should be on a Button.");

        var buttonOpeningTagEndIndex = profileSection.IndexOf('>', buttonStartIndex);
        Assert.True(buttonOpeningTagEndIndex > buttonStartIndex, $"{command} Button opening tag should be complete.");

        var buttonOpeningTag = profileSection[buttonStartIndex..(buttonOpeningTagEndIndex + 1)];
        Assert.Contains("HorizontalAlignment=\"Stretch\"", buttonOpeningTag, StringComparison.Ordinal);
        Assert.Contains("HorizontalContentAlignment=\"Center\"", buttonOpeningTag, StringComparison.Ordinal);
        Assert.Contains("Classes=\"secondary\"", buttonOpeningTag, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"96\"", buttonOpeningTag, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var searchIndex = 0;

        while ((searchIndex = text.IndexOf(value, searchIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            searchIndex += value.Length;
        }

        return count;
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
