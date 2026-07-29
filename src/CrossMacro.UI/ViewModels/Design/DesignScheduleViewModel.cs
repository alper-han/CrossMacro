
namespace CrossMacro.UI.ViewModels.Design;

public sealed class DesignScheduleViewModel : ScheduleViewModel
{
    public DesignScheduleViewModel() : this(new DesignPreviewContext()) { /* Empty */ }

    internal DesignScheduleViewModel(DesignPreviewContext context)
        : base(context.SchedulerService, context.DialogService, context.TimeProvider, context.LocalizationService)
    {
        SelectedTask = Tasks.FirstOrDefault();
    }
}
