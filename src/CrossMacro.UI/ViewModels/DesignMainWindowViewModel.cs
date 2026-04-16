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
            null)
    {
        IsPaneOpen = true;
        HasExtensionWarning = true;
        ExtensionWarning = "GNOME extension is disabled in this preview, so tray-related behavior is shown with a warning state.";
        GlobalStatus = "Macro preview loaded";
        LatestVersion = "v1.1.0";
        IsUpdateNotificationVisible = true;
        AppNotificationTitle = "Macro Preview";
        AppNotificationMessage = "Showing deterministic macro automation data for the selected page.";
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
