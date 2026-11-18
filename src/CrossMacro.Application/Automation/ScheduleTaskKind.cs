using CrossMacro.Core.Models.Automation.Scheduling;

namespace CrossMacro.Application.Automation;

public sealed class ScheduleTaskKind : AutomationTaskKind<ScheduledTask>
{
    public static ScheduleTaskKind Instance { get; } = new();

    private ScheduleTaskKind()
    {
    }

    public override string Name => "schedule";

    public override string CannotEnableReason => "Schedule task requires a macro path and valid schedule fields before it can be enabled.";

    public override Guid IdOf(ScheduledTask task) => task.Id;

    public override string NameOf(ScheduledTask task) => task.Name;

    public override ScheduledTask Copy(ScheduledTask task) => AutomationTaskSnapshots.Copy(task);

    public override void Normalize(ScheduledTask task) => task.Normalize();

    public override bool IsEnabled(ScheduledTask task) => task.IsEnabled;

    public override bool CanBeEnabled(ScheduledTask task) => task.CanBeEnabled;

    public override bool TrySetEnabled(ScheduledTask task, bool enabled) => task.TrySetEnabled(enabled);

    public override IEnumerable<string?> MacroPaths(ScheduledTask task)
    {
        yield return task.MacroFilePath;
    }
}
