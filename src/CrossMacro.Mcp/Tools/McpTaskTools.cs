namespace CrossMacro.Mcp.Tools;

public sealed class McpTaskTools(
    IScheduleCliService scheduleCliService,
    IShortcutCliService shortcutCliService,
    ITriggerCliService triggerCliService,
    McpToolAuthorization authorization)
{
    private readonly IScheduleCliService _scheduleCliService = scheduleCliService;
    private readonly IShortcutCliService _shortcutCliService = shortcutCliService;
    private readonly ITriggerCliService _triggerCliService = triggerCliService;
    private readonly McpToolAuthorization _authorization = authorization;

    [McpServerTool(Name = "schedule.list", Title = "List schedules", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public async Task<McpScheduleResult> ListSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.TaskManage);
        return capability is null
            ? CreateScheduleResult("list", await _scheduleCliService.ListAsync(cancellationToken).ConfigureAwait(false))
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
            ? CreateScheduleResult("run", await _scheduleCliService.RunAsync(taskId, cancellationToken).ConfigureAwait(false))
            : CreateScheduleResult("run", taskAuthorization);
    }

    [McpServerTool(Name = "schedule.add", Title = "Add a schedule", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> AddScheduleAsync(string name, string macroPath, string? interval = null, string? at = null, string? weekly = null, string? time = null, double? speed = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("add", new ScheduleCliOptions(ScheduleCliAction.Add, Name: name, MacroFilePath: macroPath, Interval: interval, At: at, Weekly: weekly, Time: time, Speed: speed, Enabled: enabled), cancellationToken);

    [McpServerTool(Name = "schedule.edit", Title = "Edit a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> EditScheduleAsync(string taskId, string? name = null, string? macroPath = null, string? interval = null, string? at = null, string? weekly = null, string? time = null, double? speed = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("edit", new ScheduleCliOptions(ScheduleCliAction.Edit, TaskId: taskId, Name: name, MacroFilePath: macroPath, Interval: interval, At: at, Weekly: weekly, Time: time, Speed: speed, Enabled: enabled), cancellationToken);

    [McpServerTool(Name = "schedule.remove", Title = "Remove a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> RemoveScheduleAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("remove", new ScheduleCliOptions(ScheduleCliAction.Remove, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "schedule.enable", Title = "Enable a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> EnableScheduleAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("enable", new ScheduleCliOptions(ScheduleCliAction.Enable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "schedule.disable", Title = "Disable a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> DisableScheduleAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("disable", new ScheduleCliOptions(ScheduleCliAction.Disable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "schedule.next", Title = "Get next schedule run", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> NextScheduleAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("next", new ScheduleCliOptions(ScheduleCliAction.Next, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.list", Title = "List shortcuts", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public async Task<McpShortcutResult> ListShortcutsAsync(CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.TaskManage);
        return capability is null
            ? CreateShortcutResult("list", await _shortcutCliService.ListAsync(cancellationToken).ConfigureAwait(false))
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
            ? CreateShortcutResult("run", await _shortcutCliService.RunAsync(taskId, cancellationToken).ConfigureAwait(false))
            : CreateShortcutResult("run", taskAuthorization);
    }

    [McpServerTool(Name = "shortcut.add", Title = "Add a shortcut", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> AddShortcutAsync(string name, string macroPath, string hotkey, double? speed = null, bool? loop = null, int? repeatCount = null, int? repeatDelayMs = null, int? repeatDelayMinMs = null, int? repeatDelayMaxMs = null, bool runWhileHeld = false, bool? enabled = null, IReadOnlyList<ShortcutWindowRule>? windowRules = null, bool clearWindowRules = false, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("add", new ShortcutCliOptions(ShortcutCliAction.Add, Name: name, MacroFilePath: macroPath, Hotkey: hotkey, Speed: speed, Loop: loop, RepeatCount: repeatCount, RepeatDelayMs: repeatDelayMs, RepeatDelayMinMs: repeatDelayMinMs, RepeatDelayMaxMs: repeatDelayMaxMs, RunWhileHeld: runWhileHeld, Enabled: enabled, WindowRules: windowRules, ClearWindowRules: clearWindowRules), cancellationToken);

    [McpServerTool(Name = "shortcut.edit", Title = "Edit a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> EditShortcutAsync(string taskId, string? name = null, string? macroPath = null, string? hotkey = null, double? speed = null, bool? loop = null, int? repeatCount = null, int? repeatDelayMs = null, int? repeatDelayMinMs = null, int? repeatDelayMaxMs = null, bool runWhileHeld = false, bool? enabled = null, IReadOnlyList<ShortcutWindowRule>? windowRules = null, bool clearWindowRules = false, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("edit", new ShortcutCliOptions(ShortcutCliAction.Edit, TaskId: taskId, Name: name, MacroFilePath: macroPath, Hotkey: hotkey, Speed: speed, Loop: loop, RepeatCount: repeatCount, RepeatDelayMs: repeatDelayMs, RepeatDelayMinMs: repeatDelayMinMs, RepeatDelayMaxMs: repeatDelayMaxMs, RunWhileHeld: runWhileHeld, Enabled: enabled, WindowRules: windowRules, ClearWindowRules: clearWindowRules), cancellationToken);

    [McpServerTool(Name = "shortcut.remove", Title = "Remove a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> RemoveShortcutAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("remove", new ShortcutCliOptions(ShortcutCliAction.Remove, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.enable", Title = "Enable a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> EnableShortcutAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("enable", new ShortcutCliOptions(ShortcutCliAction.Enable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.disable", Title = "Disable a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> DisableShortcutAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("disable", new ShortcutCliOptions(ShortcutCliAction.Disable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.bind", Title = "Bind a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> BindShortcutAsync(string taskId, string hotkey, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("bind", new ShortcutCliOptions(ShortcutCliAction.Bind, TaskId: taskId, Hotkey: hotkey), cancellationToken);

    [McpServerTool(Name = "trigger.list", Title = "List triggers", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public async Task<McpTriggerResult> ListTriggersAsync(CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.TaskManage);
        return capability is null
            ? CreateTriggerResult("list", await _triggerCliService.ListAsync(cancellationToken).ConfigureAwait(false))
            : CreateTriggerResult("list", capability);
    }

    [McpServerTool(Name = "trigger.add", Title = "Add a trigger", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> AddTriggerAsync(string name, string field, string value, string? matchMode = null, string? action = null, string? targetProfileId = null, string? macroPath = null, string? fireMode = null, int? cooldownMs = null, int? debounceMs = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        ExecuteTriggerAsync("add", CreateTriggerOptions(TriggerCliAction.Add, name, field, value, matchMode, action, targetProfileId, macroPath, fireMode, cooldownMs, debounceMs, enabled), cancellationToken);

    [McpServerTool(Name = "trigger.edit", Title = "Edit a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> EditTriggerAsync(string taskId, string? name = null, string? field = null, string? value = null, string? matchMode = null, string? action = null, string? targetProfileId = null, string? macroPath = null, string? fireMode = null, int? cooldownMs = null, int? debounceMs = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        ExecuteTriggerAsync("edit", CreateTriggerOptions(TriggerCliAction.Edit, name, field, value, matchMode, action, targetProfileId, macroPath, fireMode, cooldownMs, debounceMs, enabled, taskId), cancellationToken);

    [McpServerTool(Name = "trigger.remove", Title = "Remove a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> RemoveTriggerAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteTriggerAsync("remove", new TriggerCliOptions(TriggerCliAction.Remove, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "trigger.enable", Title = "Enable a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> EnableTriggerAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteTriggerAsync("enable", new TriggerCliOptions(TriggerCliAction.Enable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "trigger.disable", Title = "Disable a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> DisableTriggerAsync(string taskId, CancellationToken cancellationToken = default) =>
        ExecuteTriggerAsync("disable", new TriggerCliOptions(TriggerCliAction.Disable, TaskId: taskId), cancellationToken);

    private async Task<McpScheduleResult> ExecuteScheduleAsync(string action, ScheduleCliOptions options, CancellationToken cancellationToken)
    {
        var capability = _authorization.RequireTaskManagement(
            McpToolAuthorization.RequiresInputAutomation(options),
            requiresMacroRead: McpToolAuthorization.RequiresInputAutomation(options));
        if (capability is not null)
        {
            return CreateScheduleResult(action, capability);
        }

        if (!_authorization.TryAuthorizeCommandOptions(options, out var authorizedOptions, out var error))
        {
            return CreateScheduleResult(action, error);
        }

        if (authorizedOptions is ScheduleCliOptions authorizedSchedule
            && (authorizedSchedule.Action is ScheduleCliAction.Enable
                || authorizedSchedule is { Action: ScheduleCliAction.Edit, Enabled: true, MacroFilePath: null }))
        {
            var taskAuthorization = await _authorization.TryAuthorizeScheduleTaskMacroAsync(authorizedSchedule.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (taskAuthorization is not null)
            {
                return CreateScheduleResult(action, taskAuthorization);
            }
        }

        return CreateScheduleResult(action, await _scheduleCliService.ExecuteAsync((ScheduleCliOptions)authorizedOptions, cancellationToken).ConfigureAwait(false));
    }

    private async Task<McpShortcutResult> ExecuteShortcutAsync(string action, ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        var capability = _authorization.RequireTaskManagement(
            McpToolAuthorization.RequiresInputAutomation(options),
            requiresMacroRead: McpToolAuthorization.RequiresInputAutomation(options));
        if (capability is not null)
        {
            return CreateShortcutResult(action, capability);
        }

        if (!_authorization.TryAuthorizeCommandOptions(options, out var authorizedOptions, out var error))
        {
            return CreateShortcutResult(action, error);
        }

        if (authorizedOptions is ShortcutCliOptions authorizedShortcut
            && (authorizedShortcut.Action is ShortcutCliAction.Enable
                || authorizedShortcut is { Action: ShortcutCliAction.Edit, Enabled: true, MacroFilePath: null }))
        {
            var taskAuthorization = await _authorization.TryAuthorizeShortcutTaskMacroAsync(authorizedShortcut.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (taskAuthorization is not null)
            {
                return CreateShortcutResult(action, taskAuthorization);
            }
        }

        return CreateShortcutResult(action, await _shortcutCliService.ExecuteAsync((ShortcutCliOptions)authorizedOptions, cancellationToken).ConfigureAwait(false));
    }

    private async Task<McpTriggerResult> ExecuteTriggerAsync(string action, TriggerCliOptions options, CancellationToken cancellationToken)
    {
        var capability = _authorization.RequireTaskManagement(
            McpToolAuthorization.RequiresInputAutomation(options),
            requiresMacroRead: McpToolAuthorization.RequiresMacroRead(options));
        if (capability is not null)
        {
            return CreateTriggerResult(action, capability);
        }

        if (!_authorization.TryAuthorizeCommandOptions(options, out var authorizedOptions, out var error))
        {
            return CreateTriggerResult(action, error);
        }

        if (authorizedOptions is TriggerCliOptions authorizedTrigger
            && (authorizedTrigger.Action is TriggerCliAction.Enable
                || authorizedTrigger is { Action: TriggerCliAction.Edit, Enabled: true, MacroFilePath: null, TriggerActionVal: null }))
        {
            var taskAuthorization = await _authorization.TryAuthorizeTriggerTaskMacroAsync(authorizedTrigger.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (taskAuthorization is not null)
            {
                return CreateTriggerResult(action, taskAuthorization);
            }
        }

        return CreateTriggerResult(action, await _triggerCliService.ExecuteAsync((TriggerCliOptions)authorizedOptions, cancellationToken).ConfigureAwait(false));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308", Justification = "Enum parsing is intentionally case-insensitive for MCP option parity.")]
    private static TriggerCliOptions CreateTriggerOptions(TriggerCliAction action, string? name, string? field, string? value, string? matchMode, string? triggerAction, string? targetProfileId, string? macroPath, string? fireMode, int? cooldownMs, int? debounceMs, bool? enabled, string? taskId = null) =>
        new(action, taskId, name, TryParseEnum(field, out TriggerField parsedField) ? parsedField : null, TryParseEnum(matchMode, out TriggerMatchMode parsedMatchMode) ? parsedMatchMode : null, value, TryParseEnum(triggerAction, out TriggerOperation parsedAction) ? parsedAction : null, targetProfileId, macroPath, TryParseEnum(fireMode, out TriggerFireMode parsedFireMode) ? parsedFireMode : null, cooldownMs, debounceMs, enabled);

    private static bool TryParseEnum<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

    private static McpScheduleResult CreateScheduleResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Tasks: [], Run: null, Task: null);

    private static McpScheduleResult CreateScheduleResult(string action, CliCommandExecutionResult result)
    {
        var tasks = new List<McpScheduleTask>();
        McpScheduleTaskRun? run = null;
        McpScheduleTask? task = null;
        switch (result.Data)
        {
            case TaskListData<ScheduleTaskData> list:
                tasks.AddRange(list.Tasks.Select(ToScheduleTask));
                break;
            case ScheduleTaskRunData runData:
                run = ToScheduleTaskRun(runData);
                break;
            case ScheduleTaskData taskData:
                task = ToScheduleTask(taskData);
                break;
        }

        return new McpScheduleResult(
            Action: action,
            Outcome: McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result),
            Tasks: tasks,
            Run: run,
            Task: task);
    }

    private static McpShortcutResult CreateShortcutResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Tasks: [], Run: null, Task: null);

    private static McpShortcutResult CreateShortcutResult(string action, CliCommandExecutionResult result)
    {
        var tasks = new List<McpShortcutTask>();
        McpShortcutTaskRun? run = null;
        McpShortcutTask? task = null;
        switch (result.Data)
        {
            case TaskListData<ShortcutTaskData> list:
                tasks.AddRange(list.Tasks.Select(ToShortcutTask));
                break;
            case ShortcutTaskRunData runData:
                run = ToShortcutTaskRun(runData);
                break;
            case ShortcutTaskData taskData:
                task = ToShortcutTask(taskData);
                break;
        }

        return new McpShortcutResult(
            Action: action,
            Outcome: McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result),
            Tasks: tasks,
            Run: run,
            Task: task);
    }

    private static McpTriggerResult CreateTriggerResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Tasks: [], Task: null);

    private static McpTriggerResult CreateTriggerResult(string action, CliCommandExecutionResult result)
    {
        var tasks = new List<McpTriggerTask>();
        McpTriggerTask? task = null;
        if (result.Data is TaskListData<TriggerTaskData> list)
        {
            tasks.AddRange(list.Tasks.Select(ToTriggerTask));
        }
        else if (result.Data is TriggerTaskData taskData)
        {
            task = ToTriggerTask(taskData);
        }

        return new McpTriggerResult(
            Action: action,
            Outcome: McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result),
            Tasks: tasks,
            Task: task);
    }

    private static McpScheduleTask ToScheduleTask(ScheduleTaskData task) => new()
    {
        Id = task.Id, Name = task.Name, Enabled = task.Enabled, Type = task.Type, MacroFilePath = task.MacroFilePath, PlaybackSpeed = task.PlaybackSpeed, IntervalValue = task.IntervalValue, IntervalUnit = task.IntervalUnit, ScheduledDateTime = task.ScheduledDateTime, WeeklyDays = task.WeeklyDays, WeeklyTime = task.WeeklyTime, NextRunTime = task.NextRunTime, LastRunTime = task.LastRunTime, LastStatus = task.LastStatus,
    };

    private static McpScheduleTaskRun ToScheduleTaskRun(ScheduleTaskRunData task) => new()
    {
        Id = task.Id, Name = task.Name, Enabled = task.Enabled, MacroFilePath = task.MacroFilePath, LastRunTime = task.LastRunTime, LastStatus = task.LastStatus,
    };

    private static McpShortcutTask ToShortcutTask(ShortcutTaskData task) => new()
    {
        Id = task.Id, Name = task.Name, Enabled = task.Enabled, Hotkey = task.Hotkey, MacroFilePath = task.MacroFilePath, PlaybackSpeed = task.PlaybackSpeed, LoopEnabled = task.LoopEnabled, RunWhileHeld = task.RunWhileHeld, RepeatCount = task.RepeatCount, RepeatDelayMs = task.RepeatDelayMs, RandomRepeatDelay = task.RandomRepeatDelay, RepeatDelayMinMs = task.RepeatDelayMinMs, RepeatDelayMaxMs = task.RepeatDelayMaxMs, WindowRules = task.WindowRules.Select(static rule => new McpShortcutWindowRule { Field = rule.Field.ToString(), MatchMode = rule.MatchMode.ToString(), Value = rule.Value }).ToArray(), LastTriggeredTime = task.LastTriggeredTime, LastStatus = task.LastStatus,
    };

    private static McpShortcutTaskRun ToShortcutTaskRun(ShortcutTaskRunData task) => new()
    {
        Id = task.Id, Name = task.Name, Enabled = task.Enabled, Hotkey = task.Hotkey, MacroFilePath = task.MacroFilePath, LastTriggeredTime = task.LastTriggeredTime, LastStatus = task.LastStatus,
    };

    private static McpTriggerTask ToTriggerTask(TriggerTaskData task) => new()
    {
        Id = task.Id, Name = task.Name, Enabled = task.Enabled, Field = task.Field, MatchMode = task.MatchMode, Value = task.Value, Action = task.Action, TargetProfileId = task.TargetProfileId, MacroFilePath = task.MacroFilePath, FireMode = task.FireMode, CooldownMs = task.CooldownMs, DebounceMs = task.DebounceMs, LastTriggeredTime = task.LastTriggeredTime, LastStatus = task.LastStatus,
    };
}
