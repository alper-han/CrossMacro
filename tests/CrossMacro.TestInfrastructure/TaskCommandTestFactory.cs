using CrossMacro.Application.Automation;
using CrossMacro.Core.Models.Automation.Scheduling;
using CrossMacro.Core.Models.Automation.Shortcuts;
using CrossMacro.Core.Models.Automation.Triggers;
using CrossMacro.Core.Services.Automation.Shortcuts;

namespace CrossMacro.TestInfrastructure;

/// <summary>Builds the task command sets over a task management workflow, with hotkeys accepted as typed.</summary>
public static class TaskCommandTestFactory
{
    public static RunnableTaskCommands<ScheduledTask, ScheduleCommand> Schedule(IManageRunnableAutomationTasks<ScheduledTask> manage) =>
        new(manage, ScheduleTaskKind.Instance, ScheduleTaskCommandRules.Instance);

    public static RunnableTaskCommands<ShortcutTask, ShortcutCommand> Shortcut(IManageRunnableAutomationTasks<ShortcutTask> manage) =>
        new(manage, ShortcutTaskKind.Instance, new ShortcutTaskCommandRules(new AcceptingShortcutHotkeyNormalizer()));

    public static TaskCommands<TriggerTask, TriggerCommand> Trigger(IManageAutomationTasks<TriggerTask> manage) =>
        new(manage, TriggerTaskKind.Instance, TriggerTaskCommandRules.Instance);
}
