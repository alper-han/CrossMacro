
namespace CrossMacro.UI.ViewModels.Design;

public sealed class DesignTriggerViewModel : TriggerViewModel
{
    public DesignTriggerViewModel() : this(new DesignPreviewContext()) { /* Empty */ }

    internal DesignTriggerViewModel(DesignPreviewContext context)
        : base(context.TriggerService, context.ProfileManager, context.DialogService, context.LocalizationService, windowManager: null)
    {
        SelectedTask = Tasks.FirstOrDefault();
        OnPropertyChanged(nameof(AvailableProfiles));
    }
}
