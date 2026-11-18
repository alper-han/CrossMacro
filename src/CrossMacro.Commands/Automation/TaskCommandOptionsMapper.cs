namespace CrossMacro.Commands.Automation;

public static class TaskCommandOptionsMapper
{
    public static ScheduleCommand ToApplication(ScheduleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(
        Action: options.Action switch
        {
            ScheduleAction.Add => TaskCommandAction.Add,
            ScheduleAction.Edit => TaskCommandAction.Edit,
            ScheduleAction.Remove => TaskCommandAction.Remove,
            ScheduleAction.Enable => TaskCommandAction.Enable,
            ScheduleAction.Disable => TaskCommandAction.Disable,
            ScheduleAction.Next => TaskCommandAction.Next,
            _ => (TaskCommandAction)(-1),
        },
        TaskId: options.TaskId,
        Name: options.Name,
        MacroFilePath: options.MacroFilePath,
        Interval: options.Interval,
        At: options.At,
        Weekly: options.Weekly,
        Time: options.Time,
        Speed: options.Speed,
        Enabled: options.Enabled);
    }

    public static ShortcutCommand ToApplication(ShortcutOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(
        Action: options.Action switch
        {
            ShortcutAction.Add => TaskCommandAction.Add,
            ShortcutAction.Edit => TaskCommandAction.Edit,
            ShortcutAction.Remove => TaskCommandAction.Remove,
            ShortcutAction.Enable => TaskCommandAction.Enable,
            ShortcutAction.Disable => TaskCommandAction.Disable,
            ShortcutAction.Bind => TaskCommandAction.Bind,
            _ => (TaskCommandAction)(-1),
        },
        TaskId: options.TaskId,
        Name: options.Name,
        MacroFilePath: options.MacroFilePath,
        Hotkeys: options.Hotkeys,
        Speed: options.Speed,
        Loop: options.Loop,
        RepeatCount: options.RepeatCount,
        RepeatDelayMs: options.RepeatDelayMs,
        RepeatDelayMinMs: options.RepeatDelayMinMs,
        RepeatDelayMaxMs: options.RepeatDelayMaxMs,
        RunWhileHeld: options.RunWhileHeld,
        Enabled: options.Enabled,
        WindowRules: options.WindowRules,
        ClearWindowRules: options.ClearWindowRules,
        RunOnPressAndRelease: options.RunOnPressAndRelease,
        ReleaseMacroFilePath: options.ReleaseMacroFilePath);
    }

    public static TriggerCommand ToApplication(TriggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(
        Action: options.Action switch
        {
            TriggerTaskAction.Add => TaskCommandAction.Add,
            TriggerTaskAction.Edit => TaskCommandAction.Edit,
            TriggerTaskAction.Remove => TaskCommandAction.Remove,
            TriggerTaskAction.Enable => TaskCommandAction.Enable,
            TriggerTaskAction.Disable => TaskCommandAction.Disable,
            _ => (TaskCommandAction)(-1),
        },
        TaskId: options.TaskId,
        Name: options.Name,
        Field: options.Field,
        MatchMode: options.MatchMode,
        Value: options.Value,
        Operation: options.TriggerActionVal,
        TargetProfileId: options.TargetProfileId,
        MacroFilePath: options.MacroFilePath,
        FireMode: options.FireMode,
        CooldownMs: options.CooldownMs,
        DebounceMs: options.DebounceMs,
        Enabled: options.Enabled);
    }
}
