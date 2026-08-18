
namespace CrossMacro.UI.ViewModels;

internal sealed class MainWindowNavigationCatalog(ILocalizationService localizationService)
{
    private readonly ILocalizationService _localizationService = localizationService;

    public ObservableCollection<NavigationItem> CreateTopItems(
        RecordingViewModel recording,
        PlaybackViewModel playback,
        FilesViewModel files,
        TextExpansionViewModel textExpansion,
        ShortcutViewModel shortcuts,
        ScheduleViewModel schedule,
        TriggerViewModel triggers,
        EditorViewModel editor)
    {
        return new ObservableCollection<NavigationItem>
        {
            CreateNavigationItem("Navigation_Recording", AppIcon.Record, recording),
            CreateNavigationItem("Navigation_Playback", AppIcon.Play, playback),
            CreateNavigationItem("Navigation_Files", AppIcon.Save, files),
            CreateNavigationItem("Navigation_TextExpansion", AppIcon.EditNote, textExpansion),
            CreateNavigationItem("Navigation_Shortcuts", AppIcon.Keyboard, shortcuts),
            CreateNavigationItem("Navigation_Schedule", AppIcon.Clock, schedule),
            CreateNavigationItem("Navigation_Triggers", AppIcon.Trigger, triggers),
            CreateNavigationItem("Navigation_Editor", AppIcon.Tools, editor),
        };
    }

    public ObservableCollection<NavigationItem> CreateBottomItems(SettingsViewModel settings)
    {
        return new ObservableCollection<NavigationItem>
        {
            CreateNavigationItem("Navigation_Settings", AppIcon.Settings, settings),
        };
    }

    public void RefreshLabels(
        IEnumerable<NavigationItem> topNavigationItems,
        IEnumerable<NavigationItem> bottomNavigationItems)
    {
        RefreshLabels(topNavigationItems);
        RefreshLabels(bottomNavigationItems);
    }

    private NavigationItem CreateNavigationItem(string localizationKey, AppIcon icon, ViewModelBase viewModel)
    {
        return new NavigationItem
        {
            LocalizationKey = localizationKey,
            Label = _localizationService[localizationKey],
            Icon = icon,
            ViewModel = viewModel,
        };
    }

    private void RefreshLabels(IEnumerable<NavigationItem> navigationItems)
    {
        foreach (var item in navigationItems)
        {
            item.Label = _localizationService[item.LocalizationKey];
        }
    }
}
