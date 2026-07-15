using System.Linq;

namespace CrossMacro.UI.ViewModels;

public sealed class DesignTriggerViewModel : TriggerViewModel
{
    public DesignTriggerViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignTriggerViewModel(DesignPreviewContext context)
        : base(context.TriggerService, context.ProfileManager, context.DialogService, context.LocalizationService, windowManager: null)
    {
        SelectedTask = Tasks.FirstOrDefault();
        OnPropertyChanged(nameof(AvailableProfiles));
    }
}
