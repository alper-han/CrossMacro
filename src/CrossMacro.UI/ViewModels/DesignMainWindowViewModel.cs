using System.Linq;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// Design-time root ViewModel for XAML preview in IDE.
/// </summary>
public sealed class DesignMainWindowViewModel : MainWindowViewModel
{
    public DesignMainWindowViewModel() : this(new DesignPreviewContext())
    {
    }

    private DesignMainWindowViewModel(DesignPreviewContext context)
        : base(
            new DesignRecordingViewModel(context),
            new DesignPlaybackViewModel(context),
            new DesignFilesViewModel(context),
            new DesignTextExpansionViewModel(context),
            new DesignScheduleViewModel(context),
            new DesignShortcutViewModel(context),
            new DesignSettingsViewModel(context),
            new DesignEditorViewModel(context),
            context.HotkeyService,
            context.MousePositionProvider,
            context.EnvironmentInfoProvider,
            context.ExternalUrlOpener,
            context.LocalizationService,
            null)
    {
        IsPaneOpen = true;
        HasExtensionWarning = true;
        ExtensionWarning = "GNOME extension preview warning";
        GlobalStatus = "Preview loaded";
        LatestVersion = "v1.1.0";
        IsUpdateNotificationVisible = true;
        AppNotificationTitle = "Preview";
        AppNotificationMessage = "Showing sample macro data for the selected page.";
        AppNotificationIcon = "i";
        IsAppNotificationSuccess = true;
        IsAppNotificationVisible = true;

        var previewItem = TopNavigationItems.FirstOrDefault(item => item.Label == "Text Expansion");
        if (previewItem != null)
        {
            SelectedTopItem = previewItem;
        }
    }
}
