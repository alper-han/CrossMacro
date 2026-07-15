
namespace CrossMacro.UI.ViewModels;

public sealed class DesignScheduleViewModel : ScheduleViewModel
{
    public DesignScheduleViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignScheduleViewModel(DesignPreviewContext context)
        : base(context.SchedulerService, context.DialogService, context.TimeProvider, context.LocalizationService)
    {
        SelectedTask = Tasks.FirstOrDefault();
    }
}
