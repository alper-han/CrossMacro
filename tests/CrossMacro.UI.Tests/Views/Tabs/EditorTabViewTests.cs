namespace CrossMacro.UI.Tests.Views.Tabs;

public sealed class EditorTabViewTests
{
    [Fact]
    public void WorkspaceTabStrip_ProvidesDocumentLifecycleAndReorderingBindings()
    {
        var xaml = ReadRepoFile("src/CrossMacro.UI/Views/Tabs/EditorTabView.axaml");
        var codeBehind = ReadRepoFile("src/CrossMacro.UI/Views/Tabs/EditorTabView.axaml.cs");

        Assert.Contains("ItemsSource=\"{Binding Documents}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding ActiveDocument}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnNewTabClicked\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnCloseTabClicked\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnCloseOtherTabsClicked\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnCloseAllTabsClicked\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PointerPressed=\"OnDocumentTabPointerPressed\"", xaml, StringComparison.Ordinal);
        Assert.Contains("DoubleTapped=\"OnDocumentTabDoubleTapped\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding TabRenameText, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("OnTabRenameTextBoxKeyDown", codeBehind, StringComparison.Ordinal);
        Assert.Contains("MoveDocument(_draggedDocument, destination)", codeBehind, StringComparison.Ordinal);
    }

    [Fact]
    public void EditorDocumentView_ProvidesAnExplicitAddToPlaybackAction()
    {
        var xaml = ReadRepoFile("src/CrossMacro.UI/Views/Tabs/EditorDocumentView.axaml");

        Assert.Contains("Command=\"{Binding AddToPlaybackAsync}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding CanAddToPlayback}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Editor_AddToPlayback", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ScreenshotSection_BindsStructuredScreenshotFields()
    {
        var xaml = ReadRepoFile("src/CrossMacro.UI/Views/Tabs/EditorDocumentView.axaml");
        const string marker = "<!-- Screenshot -->";

        var markerIndex = xaml.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Screenshot section should exist in editor tab XAML.");

        var nextSectionIndex = xaml.IndexOf("<!-- Shell command -->", markerIndex, StringComparison.Ordinal);
        Assert.True(nextSectionIndex > markerIndex, "Screenshot section should appear before Shell command.");

        var section = xaml[markerIndex..nextSectionIndex];
        Assert.Contains("IsVisible=\"{Binding ShowScreenshotFields}\"", section, StringComparison.Ordinal);
        Assert.Contains("SelectedAction.ScreenshotOutputPath", section, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding BrowseScreenshotOutputPathAsync}\"", section, StringComparison.Ordinal);
        Assert.Contains("Editor_ScreenshotBrowse", section, StringComparison.Ordinal);
        Assert.DoesNotContain("IsReadOnly=\"True\"", section, StringComparison.Ordinal);
        Assert.Contains("SelectedAction.ScreenshotCopyToClipboard", section, StringComparison.Ordinal);
        Assert.Contains("SelectedAction.ScreenshotUseRegion", section, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding ShowScreenshotRegionFields}\"", section, StringComparison.Ordinal);
        Assert.Contains("SelectedAction.ScreenshotRegionX", section, StringComparison.Ordinal);
        Assert.Contains("SelectedAction.ScreenshotRegionY", section, StringComparison.Ordinal);
        Assert.Contains("SelectedAction.ScreenshotRegionWidth", section, StringComparison.Ordinal);
        Assert.Contains("SelectedAction.ScreenshotRegionHeight", section, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CaptureScreenshotRegionStartAsync}\"", section, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CaptureScreenshotRegionEndAsync}\"", section, StringComparison.Ordinal);
        Assert.Contains("Editor_CaptureRegionTopLeft", section, StringComparison.Ordinal);
        Assert.Contains("Editor_CaptureRegionBottomRight", section, StringComparison.Ordinal);
        Assert.Contains("Editor_ScreenshotWarning", section, StringComparison.Ordinal);
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
