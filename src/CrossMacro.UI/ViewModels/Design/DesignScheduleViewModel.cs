
namespace CrossMacro.UI.ViewModels.Design;

public sealed class DesignScheduleViewModel : ScheduleViewModel
{
    public DesignScheduleViewModel() : this(new DesignPreviewContext()) { /* Empty */ }

    internal DesignScheduleViewModel(DesignPreviewContext context)
        : base(new ManageAutomationTasks<ScheduledTask>(ScheduleTaskKind.Instance, context.SchedulerService), context.SchedulerService, context.DialogService, context.TimeProvider, context.LocalizationService, uiDispatcher: DesignUiDispatcher.Instance)
    {
        _ = InitializeAsync();
    }
}
