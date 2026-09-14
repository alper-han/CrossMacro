using CrossMacro.Application.Automation;
namespace CrossMacro.Mcp.Tools;

public sealed class McpTaskTools(
    IScheduleCommands scheduleCommands,
    IShortcutCommands shortcutCommands,
    ITriggerCommands triggerCommands,
    McpToolAuthorization authorization)
{
    private readonly IScheduleCommands _scheduleCommands = scheduleCommands;
    private readonly IShortcutCommands _shortcutCommands = shortcutCommands;
    private readonly ITriggerCommands _triggerCommands = triggerCommands;
    private readonly McpToolAuthorization _authorization = authorization;

    [McpServerTool(Name = "schedule.list", Title = "List schedules", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public async Task<McpScheduleResult> ListSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.TaskManage);
        return capability is null
            ? CreateScheduleResult("list", await _scheduleCommands.ListAsync(cancellationToken).ConfigureAwait(false), RedactTaskMacroPaths())
            : CreateScheduleResult("list", capability);
    }

    [McpServerTool(Name = "schedule.run", Title = "Run a schedule", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public async Task<McpScheduleResult> RunScheduleAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var capability = _authorization.RequireTaskManagement(requiresInputAutomation: true, requiresMacroRead: true);
        if (capability is not null)
        {
            return CreateScheduleResult("run", capability);
        }

        var taskAuthorization = await _authorization.TryAuthorizeScheduleTaskMacroAsync(taskId, cancellationToken).ConfigureAwait(false);
        return taskAuthorization is null
            ? await _authorization.RunWithTaskAuthorizationAsync(
                async () => CreateScheduleResult("run", await _scheduleCommands.RunAsync(taskId, cancellationToken).ConfigureAwait(false)),
                outcome => CreateScheduleResult("run", outcome)).ConfigureAwait(false)
            : CreateScheduleResult("run", taskAuthorization);
    }

    [McpServerTool(Name = "schedule.add", Title = "Add a schedule", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> AddScheduleAsync(string name, string macroPath, string? interval = null, string? at = null, string? weekly = null, string? time = null, double? speed = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("add", new ScheduleCommand(ScheduleCommandAction.Add, Name: name, MacroFilePath: macroPath, Interval: interval, At: at, Weekly: weekly, Time: time, Speed: speed, Enabled: enabled), cancellationToken);

    [McpServerTool(Name = "schedule.edit", Title = "Edit a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> EditScheduleAsync(string taskId, string? name = null, string? macroPath = null, string? interval = null, string? at = null, string? weekly = null, string? time = null, double? speed = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("edit", new ScheduleCommand(ScheduleCommandAction.Edit, TaskId: taskId, Name: name, MacroFilePath: macroPath, Interval: interval, At: at, Weekly: weekly, Time: time, Speed: speed, Enabled: enabled), cancellationToken);

    [McpServerTool(Name = "schedule.remove", Title = "Remove a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> RemoveScheduleAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("remove", new ScheduleCommand(ScheduleCommandAction.Remove, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "schedule.enable", Title = "Enable a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> EnableScheduleAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("enable", new ScheduleCommand(ScheduleCommandAction.Enable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "schedule.disable", Title = "Disable a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> DisableScheduleAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("disable", new ScheduleCommand(ScheduleCommandAction.Disable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "schedule.next", Title = "Get next schedule run", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> NextScheduleAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("next", new ScheduleCommand(ScheduleCommandAction.Next, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.list", Title = "List shortcuts", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public async Task<McpShortcutResult> ListShortcutsAsync(CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.TaskManage);
        return capability is null
            ? CreateShortcutResult("list", await _shortcutCommands.ListAsync(cancellationToken).ConfigureAwait(false), RedactTaskMacroPaths())
            : CreateShortcutResult("list", capability);
    }

    [McpServerTool(Name = "shortcut.run", Title = "Run a shortcut", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public async Task<McpShortcutResult> RunShortcutAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var capability = _authorization.RequireTaskManagement(requiresInputAutomation: true, requiresMacroRead: true);
        if (capability is not null)
        {
            return CreateShortcutResult("run", capability);
        }

        var taskAuthorization = await _authorization.TryAuthorizeShortcutTaskMacroAsync(taskId, cancellationToken).ConfigureAwait(false);
        return taskAuthorization is null
            ? await _authorization.RunWithTaskAuthorizationAsync(
                async () => CreateShortcutResult("run", await _shortcutCommands.RunAsync(taskId, cancellationToken).ConfigureAwait(false)),
                outcome => CreateShortcutResult("run", outcome)).ConfigureAwait(false)
            : CreateShortcutResult("run", taskAuthorization);
    }

    [McpServerTool(Name = "shortcut.add", Title = "Add a shortcut", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> AddShortcutAsync(string name, string macroPath, string hotkey, double? speed = null, bool? loop = null, int? repeatCount = null, int? repeatDelayMs = null, int? repeatDelayMinMs = null, int? repeatDelayMaxMs = null, bool runWhileHeld = false, bool? enabled = null, IReadOnlyList<ShortcutWindowRule>? windowRules = null, bool clearWindowRules = false, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("add", new ShortcutCommand(ShortcutCommandAction.Add, Name: name, MacroFilePath: macroPath, Hotkey: hotkey, Speed: speed, Loop: loop, RepeatCount: repeatCount, RepeatDelayMs: repeatDelayMs, RepeatDelayMinMs: repeatDelayMinMs, RepeatDelayMaxMs: repeatDelayMaxMs, RunWhileHeld: runWhileHeld, Enabled: enabled, WindowRules: windowRules, ClearWindowRules: clearWindowRules), cancellationToken);

    [McpServerTool(Name = "shortcut.edit", Title = "Edit a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> EditShortcutAsync(string taskId, string? name = null, string? macroPath = null, string? hotkey = null, double? speed = null, bool? loop = null, int? repeatCount = null, int? repeatDelayMs = null, int? repeatDelayMinMs = null, int? repeatDelayMaxMs = null, bool runWhileHeld = false, bool? enabled = null, IReadOnlyList<ShortcutWindowRule>? windowRules = null, bool clearWindowRules = false, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("edit", new ShortcutCommand(ShortcutCommandAction.Edit, TaskId: taskId, Name: name, MacroFilePath: macroPath, Hotkey: hotkey, Speed: speed, Loop: loop, RepeatCount: repeatCount, RepeatDelayMs: repeatDelayMs, RepeatDelayMinMs: repeatDelayMinMs, RepeatDelayMaxMs: repeatDelayMaxMs, RunWhileHeld: runWhileHeld, Enabled: enabled, WindowRules: windowRules, ClearWindowRules: clearWindowRules), cancellationToken);

    [McpServerTool(Name = "shortcut.remove", Title = "Remove a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> RemoveShortcutAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("remove", new ShortcutCommand(ShortcutCommandAction.Remove, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.enable", Title = "Enable a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> EnableShortcutAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("enable", new ShortcutCommand(ShortcutCommandAction.Enable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.disable", Title = "Disable a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> DisableShortcutAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("disable", new ShortcutCommand(ShortcutCommandAction.Disable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.bind", Title = "Bind a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> BindShortcutAsync(string taskId, string hotkey, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("bind", new ShortcutCommand(ShortcutCommandAction.Bind, TaskId: taskId, Hotkey: hotkey), cancellationToken);

    [McpServerTool(Name = "trigger.list", Title = "List triggers", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public async Task<McpTriggerResult> ListTriggersAsync(CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.TaskManage);
        return capability is null
            ? CreateTriggerResult("list", await _triggerCommands.ListAsync(cancellationToken).ConfigureAwait(false), RedactTaskMacroPaths())
            : CreateTriggerResult("list", capability);
    }

    [McpServerTool(Name = "trigger.add", Title = "Add a trigger", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> AddTriggerAsync(string name, string field, string value, string? matchMode = null, string? action = null, string? targetProfileId = null, string? macroPath = null, string? fireMode = null, int? cooldownMs = null, int? debounceMs = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        ExecuteTriggerAsync("add", CreateTriggerOptions(TriggerCommandAction.Add, name, field, value, matchMode, action, targetProfileId, macroPath, fireMode, cooldownMs, debounceMs, enabled), cancellationToken);

    [McpServerTool(Name = "trigger.edit", Title = "Edit a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> EditTriggerAsync(string taskId, string? name = null, string? field = null, string? value = null, string? matchMode = null, string? action = null, string? targetProfileId = null, string? macroPath = null, string? fireMode = null, int? cooldownMs = null, int? debounceMs = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        ExecuteTriggerAsync("edit", CreateTriggerOptions(TriggerCommandAction.Edit, name, field, value, matchMode, action, targetProfileId, macroPath, fireMode, cooldownMs, debounceMs, enabled, taskId), cancellationToken);

    [McpServerTool(Name = "trigger.remove", Title = "Remove a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> RemoveTriggerAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteTriggerAsync("remove", new TriggerCommand(TriggerCommandAction.Remove, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "trigger.enable", Title = "Enable a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> EnableTriggerAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteTriggerAsync("enable", new TriggerCommand(TriggerCommandAction.Enable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "trigger.disable", Title = "Disable a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> DisableTriggerAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteTriggerAsync("disable", new TriggerCommand(TriggerCommandAction.Disable, TaskId: taskId), cancellationToken);

    private async Task<McpScheduleResult> ExecuteScheduleAsync(string action, ScheduleCommand options, CancellationToken cancellationToken)
    {
        var capability = _authorization.RequireTaskManagement(
            McpToolAuthorization.RequiresInputAutomation(options),
            requiresMacroRead: McpToolAuthorization.RequiresInputAutomation(options));
        if (capability is not null)
        {
            return CreateScheduleResult(action, capability);
        }

        if (!_authorization.TryAuthorizeTaskMacroPath(options.MacroFilePath, out var authorizedMacroPath, out var error))
        {
            return CreateScheduleResult(action, error);
        }
        var authorizedOptions = options with { MacroFilePath = authorizedMacroPath };

        if (authorizedOptions.Action is ScheduleCommandAction.Enable
                || authorizedOptions is { Action: ScheduleCommandAction.Edit, Enabled: true, MacroFilePath: null })
        {
            var taskAuthorization = await _authorization.TryAuthorizeScheduleTaskMacroAsync(authorizedOptions.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (taskAuthorization is not null)
            {
                return CreateScheduleResult(action, taskAuthorization);
            }
        }

        return await _authorization.RunWithTaskAuthorizationAsync(
            async () => CreateScheduleResult(action, await _scheduleCommands.ExecuteAsync(authorizedOptions, cancellationToken).ConfigureAwait(false), RedactTaskMacroPaths()),
            outcome => CreateScheduleResult(action, outcome)).ConfigureAwait(false);
    }

    private async Task<McpShortcutResult> ExecuteShortcutAsync(string action, ShortcutCommand options, CancellationToken cancellationToken)
    {
        var capability = _authorization.RequireTaskManagement(
            McpToolAuthorization.RequiresInputAutomation(options),
            requiresMacroRead: McpToolAuthorization.RequiresInputAutomation(options));
        if (capability is not null)
        {
            return CreateShortcutResult(action, capability);
        }

        if (!_authorization.TryAuthorizeTaskMacroPath(options.MacroFilePath, out var authorizedMacroPath, out var error))
        {
            return CreateShortcutResult(action, error);
        }
        var authorizedOptions = options with { MacroFilePath = authorizedMacroPath };

        if (authorizedOptions.Action is ShortcutCommandAction.Enable
                || authorizedOptions is { Action: ShortcutCommandAction.Edit, Enabled: true, MacroFilePath: null })
        {
            var taskAuthorization = await _authorization.TryAuthorizeShortcutTaskMacroAsync(authorizedOptions.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (taskAuthorization is not null)
            {
                return CreateShortcutResult(action, taskAuthorization);
            }
        }

        return await _authorization.RunWithTaskAuthorizationAsync(
            async () => CreateShortcutResult(action, await _shortcutCommands.ExecuteAsync(authorizedOptions, cancellationToken).ConfigureAwait(false), RedactTaskMacroPaths()),
            outcome => CreateShortcutResult(action, outcome)).ConfigureAwait(false);
    }

    private async Task<McpTriggerResult> ExecuteTriggerAsync(string action, TriggerCommand options, CancellationToken cancellationToken)
    {
        var capability = _authorization.RequireTaskManagement(
            McpToolAuthorization.RequiresInputAutomation(options),
            requiresMacroRead: McpToolAuthorization.RequiresMacroRead(options));
        if (capability is not null)
        {
            return CreateTriggerResult(action, capability);
        }

        if (!_authorization.TryAuthorizeTaskMacroPath(options.MacroFilePath, out var authorizedMacroPath, out var error))
        {
            return CreateTriggerResult(action, error);
        }
        var authorizedOptions = options with { MacroFilePath = authorizedMacroPath };

        if (authorizedOptions.Action is TriggerCommandAction.Enable
                || authorizedOptions is { Action: TriggerCommandAction.Edit, Enabled: true, MacroFilePath: null, TriggerActionVal: null })
        {
            var taskAuthorization = await _authorization.TryAuthorizeTriggerTaskMacroAsync(authorizedOptions.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (taskAuthorization is not null)
            {
                return CreateTriggerResult(action, taskAuthorization);
            }
        }

        return await _authorization.RunWithTaskAuthorizationAsync(
            async () => CreateTriggerResult(action, await _triggerCommands.ExecuteAsync(authorizedOptions, cancellationToken).ConfigureAwait(false), RedactTaskMacroPaths()),
            outcome => CreateTriggerResult(action, outcome)).ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308", Justification = "Enum parsing is intentionally case-insensitive for MCP option parity.")]
    private static TriggerCommand CreateTriggerOptions(TriggerCommandAction action, string? name, string? field, string? value, string? matchMode, string? triggerAction, string? targetProfileId, string? macroPath, string? fireMode, int? cooldownMs, int? debounceMs, bool? enabled, string? taskId = null) =>
        new(action, taskId, name, TryParseEnum(field, out TriggerField parsedField) ? parsedField : null, TryParseEnum(matchMode, out TriggerMatchMode parsedMatchMode) ? parsedMatchMode : null, value, TryParseEnum(triggerAction, out TriggerOperation parsedAction) ? parsedAction : null, targetProfileId, macroPath, TryParseEnum(fireMode, out TriggerFireMode parsedFireMode) ? parsedFireMode : null, cooldownMs, debounceMs, enabled);

    private static bool TryParseEnum<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

    private static McpScheduleResult CreateScheduleResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Tasks: [], Run: null, Task: null);

    private static McpScheduleResult CreateScheduleResult(string action, TaskCommandResult<ScheduledTask> result, bool redactMacroPaths = false) => new(
        Action: action,
        Outcome: CreateOutcome(result.Success, result.Message),
        Tasks: result.Tasks?.Select(task => ToScheduleTask(task, redactMacroPaths)).ToArray() ?? [],
        Run: result.WasRun && result.Task is { } run ? ToScheduleTaskRun(run, redactMacroPaths) : null,
        Task: !result.WasRun && result.Task is { } task ? ToScheduleTask(task, redactMacroPaths) : null);

    private static McpShortcutResult CreateShortcutResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Tasks: [], Run: null, Task: null);

    private static McpShortcutResult CreateShortcutResult(string action, TaskCommandResult<ShortcutTask> result, bool redactMacroPaths = false) => new(
        Action: action,
        Outcome: CreateOutcome(result.Success, result.Message),
        Tasks: result.Tasks?.Select(task => ToShortcutTask(task, redactMacroPaths)).ToArray() ?? [],
        Run: result.WasRun && result.Task is { } run ? ToShortcutTaskRun(run, redactMacroPaths) : null,
        Task: !result.WasRun && result.Task is { } task ? ToShortcutTask(task, redactMacroPaths) : null);

    private static McpTriggerResult CreateTriggerResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Tasks: [], Task: null);

    private static McpTriggerResult CreateTriggerResult(string action, TaskCommandResult<TriggerTask> result, bool redactMacroPaths = false) => new(
        Action: action,
        Outcome: CreateOutcome(result.Success, result.Message),
        Tasks: result.Tasks?.Select(task => ToTriggerTask(task, redactMacroPaths)).ToArray() ?? [],
        Task: result.Task is { } task ? ToTriggerTask(task, redactMacroPaths) : null);

    private static McpToolOutcome CreateOutcome(bool success, string message) =>
        success ? McpToolOutcomeMapper.Success(message) : McpToolOutcomeMapper.InvalidArguments(message);

    private static McpScheduleTask ToScheduleTask(ScheduledTask task, bool redactMacroPath = false) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Enabled = task.IsEnabled,
        Type = task.Type.ToString(),
        MacroFilePath = redactMacroPath ? string.Empty : task.MacroFilePath,
        PlaybackSpeed = task.PlaybackSpeed,
        IntervalValue = task.Type is ScheduleType.Interval ? task.IntervalValue : null,
        IntervalUnit = task.Type is ScheduleType.Interval ? task.IntervalUnit.ToString() : null,
        ScheduledDateTime = task.Type is ScheduleType.SpecificTime ? task.ScheduledDateTime : null,
        WeeklyDays = task.Type is ScheduleType.Weekly ? task.WeeklyDays.ToString() : null,
        WeeklyTime = task.Type is ScheduleType.Weekly ? task.WeeklyTime.ToString() : null,
        NextRunTime = task.NextRunTime,
        LastRunTime = task.LastRunTime,
        LastStatus = task.LastStatus,
    };

    private static McpScheduleTaskRun ToScheduleTaskRun(ScheduledTask task, bool redactMacroPath = false) => new()
    {
        Id = task.Id, Name = task.Name, Enabled = task.IsEnabled, MacroFilePath = redactMacroPath ? string.Empty : task.MacroFilePath, LastRunTime = task.LastRunTime, LastStatus = task.LastStatus,
    };

    private static McpShortcutTask ToShortcutTask(ShortcutTask task, bool redactMacroPath = false) => new()
    {
        Id = task.Id, Name = task.Name, Enabled = task.IsEnabled, Hotkey = task.HotkeyString,
        MacroFilePath = redactMacroPath ? string.Empty : task.MacroFilePath, PlaybackSpeed = task.PlaybackSpeed,
        LoopEnabled = task.LoopEnabled, RunWhileHeld = task.RunWhileHeld, RepeatCount = task.RepeatCount,
        RepeatDelayMs = task.RepeatDelayMs, RandomRepeatDelay = task.UseRandomRepeatDelay,
        RepeatDelayMinMs = task.UseRandomRepeatDelay ? task.RepeatDelayMinMs : null,
        RepeatDelayMaxMs = task.UseRandomRepeatDelay ? task.RepeatDelayMaxMs : null,
        WindowRules = task.WindowRules.Select(static rule => new McpShortcutWindowRule { Field = rule.Field.ToString(), MatchMode = rule.MatchMode.ToString(), Value = rule.Value }).ToArray(),
        LastTriggeredTime = task.LastTriggeredTime, LastStatus = task.LastStatus,
    };

    private static McpShortcutTaskRun ToShortcutTaskRun(ShortcutTask task, bool redactMacroPath = false) => new()
    {
        Id = task.Id, Name = task.Name, Enabled = task.IsEnabled, Hotkey = task.HotkeyString, MacroFilePath = redactMacroPath ? string.Empty : task.MacroFilePath, LastTriggeredTime = task.LastTriggeredTime, LastStatus = task.LastStatus,
    };

    private static McpTriggerTask ToTriggerTask(TriggerTask task, bool redactMacroPath = false) => new()
    {
        Id = task.Id, Name = task.Name, Enabled = task.IsEnabled, Field = task.Field.ToString(),
        MatchMode = task.MatchMode.ToString(), Value = task.Value, Action = task.Action.ToString(),
        TargetProfileId = task.Action is TriggerOperation.SwitchProfile ? task.TargetProfileId : null,
        MacroFilePath = !redactMacroPath && task.Action is TriggerOperation.RunMacro ? task.MacroFilePath : null,
        FireMode = task.FireMode.ToString(), CooldownMs = task.CooldownMs, DebounceMs = task.DebounceMs,
        LastTriggeredTime = task.LastTriggeredTime, LastStatus = task.LastStatus,
    };

    private bool RedactTaskMacroPaths() => !_authorization.IsAnyAllowed(McpCapability.MacroRead);
}
