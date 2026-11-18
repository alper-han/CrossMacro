namespace CrossMacro.Cli.Services;

public static class TaskCommandOptionsMapper
{
    public static ScheduleCommand ToApplication(ScheduleCliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(
        Action: options.Action switch
        {
            ScheduleCliAction.Add => TaskCommandAction.Add,
            ScheduleCliAction.Edit => TaskCommandAction.Edit,
            ScheduleCliAction.Remove => TaskCommandAction.Remove,
            ScheduleCliAction.Enable => TaskCommandAction.Enable,
            ScheduleCliAction.Disable => TaskCommandAction.Disable,
            ScheduleCliAction.Next => TaskCommandAction.Next,
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

    public static ShortcutCommand ToApplication(ShortcutCliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(
        Action: options.Action switch
        {
            ShortcutCliAction.Add => TaskCommandAction.Add,
            ShortcutCliAction.Edit => TaskCommandAction.Edit,
            ShortcutCliAction.Remove => TaskCommandAction.Remove,
            ShortcutCliAction.Enable => TaskCommandAction.Enable,
            ShortcutCliAction.Disable => TaskCommandAction.Disable,
            ShortcutCliAction.Bind => TaskCommandAction.Bind,
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

    public static TriggerCommand ToApplication(TriggerCliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(
        Action: options.Action switch
        {
            TriggerCliAction.Add => TaskCommandAction.Add,
            TriggerCliAction.Edit => TaskCommandAction.Edit,
            TriggerCliAction.Remove => TaskCommandAction.Remove,
            TriggerCliAction.Enable => TaskCommandAction.Enable,
            TriggerCliAction.Disable => TaskCommandAction.Disable,
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
