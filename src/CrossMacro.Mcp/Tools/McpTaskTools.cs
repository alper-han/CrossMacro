using CrossMacro.Application.Automation;
namespace CrossMacro.Mcp.Tools;

public sealed class McpTaskTools(
    IRunnableTaskCommands<ScheduledTask, ScheduleCommand> scheduleCommands,
    IRunnableTaskCommands<ShortcutTask, ShortcutCommand> shortcutCommands,
    ITaskCommands<TriggerTask, TriggerCommand> triggerCommands,
    McpToolAuthorization authorization)
{
    private readonly McpScheduleToolKind _schedules = new(authorization, scheduleCommands);
    private readonly McpShortcutToolKind _shortcuts = new(authorization, shortcutCommands);
    private readonly McpTriggerToolKind _triggers = new(authorization, triggerCommands);

    [McpServerTool(Name = "schedule.list", Title = "List schedules", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> ListSchedulesAsync(CancellationToken cancellationToken = default) =>
        _schedules.ListAsync(cancellationToken);

    [McpServerTool(Name = "schedule.run", Title = "Run a schedule", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> RunScheduleAsync(string taskId, CancellationToken cancellationToken = default) =>
        _schedules.RunAsync(taskId, cancellationToken);

    [McpServerTool(Name = "schedule.add", Title = "Add a schedule", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> AddScheduleAsync(string name, string macroPath, string? interval = null, string? at = null, string? weekly = null, string? time = null, double? speed = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        _schedules.ExecuteAsync("add", new ScheduleCommand(TaskCommandAction.Add, Name: name, MacroFilePath: macroPath, Interval: interval, At: at, Weekly: weekly, Time: time, Speed: speed, Enabled: enabled), cancellationToken);

    [McpServerTool(Name = "schedule.edit", Title = "Edit a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> EditScheduleAsync(string taskId, string? name = null, string? macroPath = null, string? interval = null, string? at = null, string? weekly = null, string? time = null, double? speed = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        _schedules.ExecuteAsync("edit", new ScheduleCommand(TaskCommandAction.Edit, TaskId: taskId, Name: name, MacroFilePath: macroPath, Interval: interval, At: at, Weekly: weekly, Time: time, Speed: speed, Enabled: enabled), cancellationToken);

    [McpServerTool(Name = "schedule.remove", Title = "Remove a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> RemoveScheduleAsync(string taskId, CancellationToken cancellationToken = default) =>
        _schedules.ExecuteAsync("remove", new ScheduleCommand(TaskCommandAction.Remove, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "schedule.enable", Title = "Enable a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> EnableScheduleAsync(string taskId, CancellationToken cancellationToken = default) =>
        _schedules.ExecuteAsync("enable", new ScheduleCommand(TaskCommandAction.Enable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "schedule.disable", Title = "Disable a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> DisableScheduleAsync(string taskId, CancellationToken cancellationToken = default) =>
        _schedules.ExecuteAsync("disable", new ScheduleCommand(TaskCommandAction.Disable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "schedule.next", Title = "Get next schedule run", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> NextScheduleAsync(string taskId, CancellationToken cancellationToken = default) =>
        _schedules.ExecuteAsync("next", new ScheduleCommand(TaskCommandAction.Next, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.list", Title = "List shortcuts", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> ListShortcutsAsync(CancellationToken cancellationToken = default) =>
        _shortcuts.ListAsync(cancellationToken);

    [McpServerTool(Name = "shortcut.run", Title = "Run a shortcut", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> RunShortcutAsync(string taskId, CancellationToken cancellationToken = default) =>
        _shortcuts.RunAsync(taskId, cancellationToken);

    [McpServerTool(Name = "shortcut.add", Title = "Add a shortcut", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> AddShortcutAsync(string name, string macroPath, IReadOnlyList<string> hotkeys, double? speed = null, bool? loop = null, int? repeatCount = null, int? repeatDelayMs = null, int? repeatDelayMinMs = null, int? repeatDelayMaxMs = null, bool runWhileHeld = false, bool? enabled = null, IReadOnlyList<ShortcutWindowRule>? windowRules = null, bool clearWindowRules = false, bool? runOnPressAndRelease = null, string? releaseMacroFilePath = null, CancellationToken cancellationToken = default) =>
        _shortcuts.ExecuteAsync("add", new ShortcutCommand(TaskCommandAction.Add, Name: name, MacroFilePath: macroPath, Hotkeys: hotkeys, Speed: speed, Loop: loop, RepeatCount: repeatCount, RepeatDelayMs: repeatDelayMs, RepeatDelayMinMs: repeatDelayMinMs, RepeatDelayMaxMs: repeatDelayMaxMs, RunWhileHeld: runWhileHeld, Enabled: enabled, WindowRules: windowRules, ClearWindowRules: clearWindowRules, RunOnPressAndRelease: runOnPressAndRelease, ReleaseMacroFilePath: releaseMacroFilePath), cancellationToken);

    [McpServerTool(Name = "shortcut.edit", Title = "Edit a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> EditShortcutAsync(string taskId, string? name = null, string? macroPath = null, IReadOnlyList<string>? hotkeys = null, double? speed = null, bool? loop = null, int? repeatCount = null, int? repeatDelayMs = null, int? repeatDelayMinMs = null, int? repeatDelayMaxMs = null, bool runWhileHeld = false, bool? enabled = null, IReadOnlyList<ShortcutWindowRule>? windowRules = null, bool clearWindowRules = false, bool? runOnPressAndRelease = null, string? releaseMacroFilePath = null, CancellationToken cancellationToken = default) =>
        _shortcuts.ExecuteAsync("edit", new ShortcutCommand(TaskCommandAction.Edit, TaskId: taskId, Name: name, MacroFilePath: macroPath, Hotkeys: hotkeys, Speed: speed, Loop: loop, RepeatCount: repeatCount, RepeatDelayMs: repeatDelayMs, RepeatDelayMinMs: repeatDelayMinMs, RepeatDelayMaxMs: repeatDelayMaxMs, RunWhileHeld: runWhileHeld, Enabled: enabled, WindowRules: windowRules, ClearWindowRules: clearWindowRules, RunOnPressAndRelease: runOnPressAndRelease, ReleaseMacroFilePath: releaseMacroFilePath), cancellationToken);

    [McpServerTool(Name = "shortcut.remove", Title = "Remove a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> RemoveShortcutAsync(string taskId, CancellationToken cancellationToken = default) =>
        _shortcuts.ExecuteAsync("remove", new ShortcutCommand(TaskCommandAction.Remove, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.enable", Title = "Enable a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> EnableShortcutAsync(string taskId, CancellationToken cancellationToken = default) =>
        _shortcuts.ExecuteAsync("enable", new ShortcutCommand(TaskCommandAction.Enable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.disable", Title = "Disable a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> DisableShortcutAsync(string taskId, CancellationToken cancellationToken = default) =>
        _shortcuts.ExecuteAsync("disable", new ShortcutCommand(TaskCommandAction.Disable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.bind", Title = "Bind a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> BindShortcutAsync(string taskId, IReadOnlyList<string> hotkeys, CancellationToken cancellationToken = default) =>
        _shortcuts.ExecuteAsync("bind", new ShortcutCommand(TaskCommandAction.Bind, TaskId: taskId, Hotkeys: hotkeys), cancellationToken);

    [McpServerTool(Name = "trigger.list", Title = "List triggers", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> ListTriggersAsync(CancellationToken cancellationToken = default) =>
        _triggers.ListAsync(cancellationToken);

    [McpServerTool(Name = "trigger.add", Title = "Add a trigger", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> AddTriggerAsync(string name, string field, string value, string? matchMode = null, string? action = null, string? targetProfileId = null, string? macroPath = null, string? fireMode = null, int? cooldownMs = null, int? debounceMs = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        _triggers.ExecuteAsync("add", CreateTriggerOptions(TaskCommandAction.Add, name, field, value, matchMode, action, targetProfileId, macroPath, fireMode, cooldownMs, debounceMs, enabled), cancellationToken);

    [McpServerTool(Name = "trigger.edit", Title = "Edit a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> EditTriggerAsync(string taskId, string? name = null, string? field = null, string? value = null, string? matchMode = null, string? action = null, string? targetProfileId = null, string? macroPath = null, string? fireMode = null, int? cooldownMs = null, int? debounceMs = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        _triggers.ExecuteAsync("edit", CreateTriggerOptions(TaskCommandAction.Edit, name, field, value, matchMode, action, targetProfileId, macroPath, fireMode, cooldownMs, debounceMs, enabled, taskId), cancellationToken);

    [McpServerTool(Name = "trigger.remove", Title = "Remove a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> RemoveTriggerAsync(string taskId, CancellationToken cancellationToken = default) =>
        _triggers.ExecuteAsync("remove", new TriggerCommand(TaskCommandAction.Remove, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "trigger.enable", Title = "Enable a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> EnableTriggerAsync(string taskId, CancellationToken cancellationToken = default) =>
        _triggers.ExecuteAsync("enable", new TriggerCommand(TaskCommandAction.Enable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "trigger.disable", Title = "Disable a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> DisableTriggerAsync(string taskId, CancellationToken cancellationToken = default) =>
        _triggers.ExecuteAsync("disable", new TriggerCommand(TaskCommandAction.Disable, TaskId: taskId), cancellationToken);

    private static TriggerCommand CreateTriggerOptions(TaskCommandAction action, string? name, string? field, string? value, string? matchMode, string? triggerAction, string? targetProfileId, string? macroPath, string? fireMode, int? cooldownMs, int? debounceMs, bool? enabled, string? taskId = null) =>
        new(action, taskId, name, CommandValueTokens.TryParseDefinedEnum(field, out TriggerField parsedField) ? parsedField : null, CommandValueTokens.TryParseDefinedEnum(matchMode, out TriggerMatchMode parsedMatchMode) ? parsedMatchMode : null, value, CommandValueTokens.TryParseDefinedEnum(triggerAction, out TriggerOperation parsedAction) ? parsedAction : null, targetProfileId, macroPath, CommandValueTokens.TryParseDefinedEnum(fireMode, out TriggerFireMode parsedFireMode) ? parsedFireMode : null, cooldownMs, debounceMs, enabled);
}
