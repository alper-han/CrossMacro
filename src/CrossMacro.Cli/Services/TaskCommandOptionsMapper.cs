namespace CrossMacro.Cli.Services;

public static class TaskCommandOptionsMapper
{
    public static ScheduleCommand ToApplication(ScheduleCliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(
        Action: options.Action switch
        {
            ScheduleCliAction.Add => ScheduleCommandAction.Add,
            ScheduleCliAction.Edit => ScheduleCommandAction.Edit,
            ScheduleCliAction.Remove => ScheduleCommandAction.Remove,
            ScheduleCliAction.Enable => ScheduleCommandAction.Enable,
            ScheduleCliAction.Disable => ScheduleCommandAction.Disable,
            ScheduleCliAction.Next => ScheduleCommandAction.Next,
            _ => (ScheduleCommandAction)(-1),
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
            ShortcutCliAction.Add => ShortcutCommandAction.Add,
            ShortcutCliAction.Edit => ShortcutCommandAction.Edit,
            ShortcutCliAction.Remove => ShortcutCommandAction.Remove,
            ShortcutCliAction.Enable => ShortcutCommandAction.Enable,
            ShortcutCliAction.Disable => ShortcutCommandAction.Disable,
            ShortcutCliAction.Bind => ShortcutCommandAction.Bind,
            _ => (ShortcutCommandAction)(-1),
        },
        TaskId: options.TaskId,
        Name: options.Name,
        MacroFilePath: options.MacroFilePath,
        Hotkey: options.Hotkey,
        Speed: options.Speed,
        Loop: options.Loop,
        RepeatCount: options.RepeatCount,
        RepeatDelayMs: options.RepeatDelayMs,
        RepeatDelayMinMs: options.RepeatDelayMinMs,
        RepeatDelayMaxMs: options.RepeatDelayMaxMs,
        RunWhileHeld: options.RunWhileHeld,
        Enabled: options.Enabled,
        WindowRules: options.WindowRules,
        ClearWindowRules: options.ClearWindowRules);
    }

    public static TriggerCommand ToApplication(TriggerCliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(
        Action: options.Action switch
        {
            TriggerCliAction.Add => TriggerCommandAction.Add,
            TriggerCliAction.Edit => TriggerCommandAction.Edit,
            TriggerCliAction.Remove => TriggerCommandAction.Remove,
            TriggerCliAction.Enable => TriggerCommandAction.Enable,
            TriggerCliAction.Disable => TriggerCommandAction.Disable,
            _ => (TriggerCommandAction)(-1),
        },
        TaskId: options.TaskId,
        Name: options.Name,
        Field: options.Field,
        MatchMode: options.MatchMode,
        Value: options.Value,
        TriggerActionVal: options.TriggerActionVal,
        TargetProfileId: options.TargetProfileId,
        MacroFilePath: options.MacroFilePath,
        FireMode: options.FireMode,
        CooldownMs: options.CooldownMs,
        DebounceMs: options.DebounceMs,
        Enabled: options.Enabled);
    }
}
