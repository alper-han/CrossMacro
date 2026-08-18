namespace CrossMacro.UI.Tests.Views.Tabs;

public sealed class DesignViewModelNamespaceTests
{
    [Theory]
    [InlineData("src/CrossMacro.UI/Views/MainWindow.axaml", "DesignMainWindowViewModel")]
    [InlineData("src/CrossMacro.UI/Views/Tabs/EditorTabView.axaml", "DesignEditorViewModel")]
    [InlineData("src/CrossMacro.UI/Views/Tabs/SettingsTabView.axaml", "DesignSettingsViewModel")]
    [InlineData("src/CrossMacro.UI/Views/Tabs/FilesTabView.axaml", "DesignFilesViewModel")]
    [InlineData("src/CrossMacro.UI/Views/Tabs/TextExpansionTabView.axaml", "DesignTextExpansionViewModel")]
    [InlineData("src/CrossMacro.UI/Views/Tabs/TriggerTabView.axaml", "DesignTriggerViewModel")]
    [InlineData("src/CrossMacro.UI/Views/Tabs/RecordingTabView.axaml", "DesignRecordingViewModel")]
    [InlineData("src/CrossMacro.UI/Views/Tabs/ShortcutTabView.axaml", "DesignShortcutViewModel")]
    [InlineData("src/CrossMacro.UI/Views/Tabs/PlaybackTabView.axaml", "DesignPlaybackViewModel")]
    [InlineData("src/CrossMacro.UI/Views/Tabs/ScheduleTabView.axaml", "DesignScheduleViewModel")]
    public void DesignDataContext_UsesDesignVmNamespace(string relativePath, string expectedDesignVm)
    {
        var xaml = ReadRepoFile(relativePath);

        Assert.Contains("xmlns:designVm=\"using:CrossMacro.UI.ViewModels.Design\"", xaml, StringComparison.Ordinal);
        Assert.Contains($"<designVm:{expectedDesignVm}/>", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain($"<vm:{expectedDesignVm}/>", xaml, StringComparison.Ordinal);
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
