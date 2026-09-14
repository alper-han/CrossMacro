
namespace CrossMacro.UI.ViewModels.Design;

public sealed class DesignTriggerViewModel : TriggerViewModel
{
    public DesignTriggerViewModel() : this(new DesignPreviewContext()) { /* Empty */ }

    internal DesignTriggerViewModel(DesignPreviewContext context)
        : base(new ManageTrigger(context.TriggerService, context.TriggerService), context.TriggerService, context.ProfileManager, context.DialogService, context.LocalizationService, windowManager: null, uiDispatcher: DesignUiDispatcher.Instance)
    {
        _ = InitializeAsync();
        OnPropertyChanged(nameof(AvailableProfiles));
    }
}
