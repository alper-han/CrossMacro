namespace CrossMacro.UI.Tests.Views.Tabs;

public sealed class BindingNullSafetyTests
{
    [Theory]
    [InlineData("src/CrossMacro.UI/Views/Tabs/ScheduleTabView.axaml")]
    [InlineData("src/CrossMacro.UI/Views/Tabs/ShortcutTabView.axaml")]
    public void SelectedTaskBindings_NavigateNullableSelectionSafely(string relativePath)
    {
        var xaml = ReadRepoFile(relativePath);

        Assert.DoesNotContain("SelectedTask.", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedTask?.", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void TextExpansionParentBindings_NavigateNullableRootDataContextSafely()
    {
        var xaml = ReadRepoFile("src/CrossMacro.UI/Views/Tabs/TextExpansionTabView.axaml");

        Assert.Contains("DataContext)?.ToggleExpansionCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("DataContext)?.IsPasteMethodVisible", xaml, StringComparison.Ordinal);
        Assert.Contains("DataContext)?.RemoveExpansionCommand", xaml, StringComparison.Ordinal);
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
