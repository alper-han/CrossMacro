namespace CrossMacro.UI.Tests.ViewModels;

public sealed class MainWindowNavigationCatalogTests
{
    [Fact]
    public void CreateItems_ExposeExpectedNavigationMetadataAndPages()
    {
        using var harness = MainWindowHarness.Create();
        var catalog = new MainWindowNavigationCatalog(harness.LocalizationService);

        var topItems = CreateTopItems(catalog, harness);
        var bottomItems = catalog.CreateBottomItems(harness.Settings);

        _ = topItems.Select(item => (item.LocalizationKey, item.Label, item.ViewModel)).Should().Equal(
            ("Navigation_Recording", "[Navigation_Recording]", harness.Recording),
            ("Navigation_Playback", "[Navigation_Playback]", harness.Playback),
            ("Navigation_Files", "[Navigation_Files]", harness.Files),
            ("Navigation_TextExpansion", "[Navigation_TextExpansion]", harness.TextExpansion),
            ("Navigation_Shortcuts", "[Navigation_Shortcuts]", harness.Shortcut),
            ("Navigation_Schedule", "[Navigation_Schedule]", harness.Schedule),
            ("Navigation_Triggers", "[Navigation_Triggers]", harness.Trigger),
            ("Navigation_Editor", "[Navigation_Editor]", harness.ViewModel.Editor));
        _ = topItems.Should().OnlyContain(item => Enum.IsDefined(item.Icon));

        _ = bottomItems.Select(item => (item.LocalizationKey, item.Label, item.ViewModel)).Should().Equal(
            ("Navigation_Settings", "[Navigation_Settings]", harness.Settings));
        _ = bottomItems.Should().OnlyContain(item => Enum.IsDefined(item.Icon));
    }

    [Fact]
    public void RefreshLabels_UpdatesLabelsByLocalizationKey()
    {
        using var harness = MainWindowHarness.Create();
        var catalog = new MainWindowNavigationCatalog(harness.LocalizationService);
        var topItems = CreateTopItems(catalog, harness);
        var bottomItems = catalog.CreateBottomItems(harness.Settings);
        _ = harness.LocalizationService["Navigation_Recording"].Returns("[Navigation_Recording:updated]");
        _ = harness.LocalizationService["Navigation_Settings"].Returns("[Navigation_Settings:updated]");

        catalog.RefreshLabels(topItems, bottomItems);

        _ = topItems[0].Label.Should().Be("[Navigation_Recording:updated]");
        _ = bottomItems[0].Label.Should().Be("[Navigation_Settings:updated]");
    }

    private static ObservableCollection<NavigationItem> CreateTopItems(MainWindowNavigationCatalog catalog, MainWindowHarness harness) =>
        catalog.CreateTopItems(
            harness.Recording,
            harness.Playback,
            harness.Files,
            harness.TextExpansion,
            harness.Shortcut,
            harness.Schedule,
            harness.Trigger,
            harness.ViewModel.Editor);
}
