
namespace CrossMacro.UI.ViewModels.Design;

/// <summary>
/// Design-time root ViewModel for XAML preview in IDE.
/// </summary>
public sealed class DesignMainWindowViewModel : MainWindowViewModel
{
    public DesignMainWindowViewModel() : this(new DesignPreviewContext()) { /* Empty */ }

    private DesignMainWindowViewModel(DesignPreviewContext context)
        : base(
            new DesignRecordingViewModel(context),
            new DesignPlaybackViewModel(context),
            new DesignFilesViewModel(context),
            new DesignTextExpansionViewModel(context),
            new DesignScheduleViewModel(context),
            new DesignShortcutViewModel(context),
            new DesignTriggerViewModel(context),
            new DesignSettingsViewModel(context),
            new DesignEditorWorkspaceViewModel(context),
            context.HotkeyService,
            context.EnvironmentInfoProvider,
            context.ExternalUrlOpener,
            context.LocalizationService,
extensionNotifier: null, uiDispatcher: DesignUiDispatcher.Instance)
    {
        IsPaneOpen = true;
        ExtensionWarning = "GNOME extension preview warning";
        GlobalStatus = "Preview loaded";
        AppNotificationTitle = "Preview";
        AppNotificationMessage = "Showing sample macro data for the selected page.";
        NotificationSeverity = AppNotificationSeverity.Success;
        IsAppNotificationVisible = true;

        var previewItem = TopNavigationItems.FirstOrDefault(item => item.LocalizationKey is "Navigation_TextExpansion");
        if (previewItem is not null)
        {
            SelectedTopItem = previewItem;
        }
    }
}
