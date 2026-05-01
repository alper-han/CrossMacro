using System.Collections.Generic;
using System.Collections.ObjectModel;
using CrossMacro.Core.Services;
using CrossMacro.UI.Models;

namespace CrossMacro.UI.ViewModels;

internal sealed class MainWindowNavigationCatalog
{
    private readonly ILocalizationService _localizationService;

    public MainWindowNavigationCatalog(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public ObservableCollection<NavigationItem> CreateTopItems(
        RecordingViewModel recording,
        PlaybackViewModel playback,
        FilesViewModel files,
        TextExpansionViewModel textExpansion,
        ShortcutViewModel shortcuts,
        ScheduleViewModel schedule,
        EditorViewModel editor)
    {
        return new ObservableCollection<NavigationItem>
        {
            CreateNavigationItem("Navigation_Recording", "🔴", recording),
            CreateNavigationItem("Navigation_Playback", "▶️", playback),
            CreateNavigationItem("Navigation_Files", "💾", files),
            CreateNavigationItem("Navigation_TextExpansion", "📝", textExpansion),
            CreateNavigationItem("Navigation_Shortcuts", "⌨️", shortcuts),
            CreateNavigationItem("Navigation_Schedule", "🕐", schedule),
            CreateNavigationItem("Navigation_Editor", "🛠️", editor)
        };
    }

    public ObservableCollection<NavigationItem> CreateBottomItems(SettingsViewModel settings)
    {
        return new ObservableCollection<NavigationItem>
        {
            CreateNavigationItem("Navigation_Settings", "⚙️", settings)
        };
    }

    public void RefreshLabels(
        IEnumerable<NavigationItem> topNavigationItems,
        IEnumerable<NavigationItem> bottomNavigationItems)
    {
        RefreshLabels(topNavigationItems);
        RefreshLabels(bottomNavigationItems);
    }

    private NavigationItem CreateNavigationItem(string localizationKey, string icon, ViewModelBase viewModel)
    {
        return new NavigationItem
        {
            LocalizationKey = localizationKey,
            Label = _localizationService[localizationKey],
            Icon = icon,
            ViewModel = viewModel
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
