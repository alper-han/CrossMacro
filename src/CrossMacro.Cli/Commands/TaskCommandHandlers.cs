namespace CrossMacro.Cli.Commands;

/// <summary>Binds the generic task handlers to the schedule, shortcut and trigger kinds.</summary>
public static class TaskCommandHandlers
{
    public static TaskCommandHandler<ScheduleOptions, ScheduledTask, ScheduleCommand> Schedule(ITaskCommands<ScheduledTask, ScheduleCommand> commands) =>
        new(commands, TaskCommandOptionsMapper.ToApplication, TaskCliResultMapper.Schedule);

    public static TaskListCommandHandler<ScheduleListOptions, ScheduledTask, ScheduleCommand> ScheduleList(ITaskCommands<ScheduledTask, ScheduleCommand> commands) =>
        new(commands, TaskCliResultMapper.Schedule);

    public static TaskRunCommandHandler<ScheduleRunOptions, ScheduledTask, ScheduleCommand> ScheduleRun(IRunnableTaskCommands<ScheduledTask, ScheduleCommand> commands) =>
        new(commands, static options => options.TaskId, TaskCliResultMapper.Schedule);

    public static TaskCommandHandler<ShortcutOptions, ShortcutTask, ShortcutCommand> Shortcut(ITaskCommands<ShortcutTask, ShortcutCommand> commands) =>
        new(commands, TaskCommandOptionsMapper.ToApplication, TaskCliResultMapper.Shortcut);

    public static TaskListCommandHandler<ShortcutListOptions, ShortcutTask, ShortcutCommand> ShortcutList(ITaskCommands<ShortcutTask, ShortcutCommand> commands) =>
        new(commands, TaskCliResultMapper.Shortcut);

    public static TaskRunCommandHandler<ShortcutRunOptions, ShortcutTask, ShortcutCommand> ShortcutRun(IRunnableTaskCommands<ShortcutTask, ShortcutCommand> commands) =>
        new(commands, static options => options.TaskId, TaskCliResultMapper.Shortcut);

    public static TaskCommandHandler<TriggerOptions, TriggerTask, TriggerCommand> Trigger(ITaskCommands<TriggerTask, TriggerCommand> commands) =>
        new(commands, TaskCommandOptionsMapper.ToApplication, TaskCliResultMapper.Trigger);

    public static TaskListCommandHandler<TriggerListOptions, TriggerTask, TriggerCommand> TriggerList(ITaskCommands<TriggerTask, TriggerCommand> commands) =>
        new(commands, TaskCliResultMapper.Trigger);
}
