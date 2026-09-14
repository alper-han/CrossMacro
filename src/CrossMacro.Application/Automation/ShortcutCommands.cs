using System.Globalization;

namespace CrossMacro.Application.Automation;

public sealed class ShortcutCommands(IManageShortcut manageShortcut) : IShortcutCommands
{
    private readonly IManageShortcut _manageShortcut = manageShortcut;

    public async Task<TaskCommandResult<ShortcutTask>> ListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _manageShortcut.ListAsync(cancellationToken).ConfigureAwait(false);
            return new(Success: true, $"Loaded {result.Tasks.Count} shortcut task(s).", [], Tasks: result.Tasks);
        }
        catch (TaskScopeUnavailableException ex)
        {
            return TaskCommandResult.Fail<ShortcutTask>(ex.Message);
        }
    }

    public async Task<TaskCommandResult<ShortcutTask>> RunAsync(string taskId, CancellationToken cancellationToken)
    {
        try
        {
            return await RunCoreAsync(taskId, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is TaskScopeConflictException or TaskScopeUnavailableException)
        {
            return TaskCommandResult.Fail<ShortcutTask>(ex.Message);
        }
    }

    private async Task<TaskCommandResult<ShortcutTask>> RunCoreAsync(string taskId, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }
        await _manageShortcut.RunAsync(new TaskRequest(parsed.Task!.Id, ExpectedScopeGeneration: parsed.ScopeGeneration), cancellationToken).ConfigureAwait(false);
        return new(Success: true, "Shortcut task executed.", [], Task: parsed.Task, WasRun: true);
    }

    public async Task<TaskCommandResult<ShortcutTask>> ExecuteAsync(ShortcutCommand options, CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteCoreAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is TaskScopeConflictException or TaskScopeUnavailableException)
        {
            return TaskCommandResult.Fail<ShortcutTask>(ex.Message);
        }
    }

    private async Task<TaskCommandResult<ShortcutTask>> ExecuteCoreAsync(ShortcutCommand options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Action switch
        {
            ShortcutCommandAction.Add => await AddAsync(options, cancellationToken).ConfigureAwait(false),
            ShortcutCommandAction.Edit => await EditAsync(options, cancellationToken).ConfigureAwait(false),
            ShortcutCommandAction.Remove => await RemoveAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false),
            ShortcutCommandAction.Enable => await SetEnabledAsync(options.TaskId ?? string.Empty, enabled: true, cancellationToken).ConfigureAwait(false),
            ShortcutCommandAction.Disable => await SetEnabledAsync(options.TaskId ?? string.Empty, enabled: false, cancellationToken).ConfigureAwait(false),
            ShortcutCommandAction.Bind => await BindAsync(options, cancellationToken).ConfigureAwait(false),
            _ => TaskCommandResult.Fail<ShortcutTask>("Unknown shortcut action."),
        };
    }

    private async Task<TaskCommandResult<ShortcutTask>> AddAsync(ShortcutCommand options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scope = await _manageShortcut.ListAsync(cancellationToken).ConfigureAwait(false);
        var task = new ShortcutTask
        {
            Name = options.Name ?? string.Empty,
            MacroFilePath = options.MacroFilePath ?? string.Empty,
            HotkeyString = options.Hotkey ?? string.Empty,
        };
        ApplyOptions(task, options);

        if (options.Enabled is not null)
        {
            _ = task.TrySetEnabled(options.Enabled.Value);
        }

        task = await _manageShortcut.AddAsync(task, scope.ScopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<ShortcutTask>($"Shortcut task added: {task.Name}.", task);
    }

    private async Task<TaskCommandResult<ShortcutTask>> EditAsync(ShortcutCommand options, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = AutomationTaskSnapshots.Copy(parsed.Task!);
        if (!string.IsNullOrWhiteSpace(options.Name))
        {
            task.Name = options.Name;
        }

        if (!string.IsNullOrWhiteSpace(options.MacroFilePath))
        {
            task.MacroFilePath = options.MacroFilePath;
        }

        if (!string.IsNullOrWhiteSpace(options.Hotkey))
        {
            task.HotkeyString = options.Hotkey;
        }

        ApplyOptions(task, options);
        if (options.Enabled is not null)
        {
            _ = task.TrySetEnabled(options.Enabled.Value);
        }

        cancellationToken.ThrowIfCancellationRequested();
        task = await _manageShortcut.UpdateAsync(task, parsed.ScopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<ShortcutTask>($"Shortcut task updated: {task.Name}.", task);
    }

    private async Task<TaskCommandResult<ShortcutTask>> RemoveAsync(string taskId, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
        cancellationToken.ThrowIfCancellationRequested();
        _ = await _manageShortcut.RemoveAsync(new TaskRequest(task.Id, ExpectedScopeGeneration: parsed.ScopeGeneration), cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<ShortcutTask>($"Shortcut task removed: {task.Name}.", task);
    }

    private async Task<TaskCommandResult<ShortcutTask>> SetEnabledAsync(string taskId, bool enabled, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
        if (enabled && !task.CanBeEnabled)
        {
            return TaskCommandResult.Fail<ShortcutTask>(
                "Shortcut task cannot be enabled.",
                ["Shortcut task requires a macro path, hotkey, and valid window rules before it can be enabled."]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _ = await _manageShortcut.SetEnabledAsync(new TaskRequest(task.Id, enabled, parsed.ScopeGeneration), cancellationToken).ConfigureAwait(false);
        task.IsEnabled = enabled;
        var verb = enabled ? "enabled" : "disabled";
        return TaskCommandResult.Ok<ShortcutTask>($"Shortcut task {verb}: {task.Name}.", task);
    }

    private async Task<TaskCommandResult<ShortcutTask>> BindAsync(ShortcutCommand options, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = AutomationTaskSnapshots.Copy(parsed.Task!);
        task.HotkeyString = options.Hotkey ?? string.Empty;
        cancellationToken.ThrowIfCancellationRequested();
        task = await _manageShortcut.UpdateAsync(task, parsed.ScopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<ShortcutTask>($"Shortcut task bound: {task.Name}.", task);
    }

    private async Task<(ShortcutTask? Task, TaskCommandResult<ShortcutTask>? Result, long ScopeGeneration)> LoadAndFindAsync(string taskId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParse(taskId, out var id))
        {
            return (null, TaskCommandResult.Fail<ShortcutTask>("Invalid shortcut task id format.", [$"Task id is not a valid GUID: {taskId}"]), 0);
        }
        var tasks = await _manageShortcut.ListAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var task = tasks.Tasks.FirstOrDefault(candidate => candidate.Id == id);
        return task is null
            ? (null, TaskCommandResult.Fail<ShortcutTask>("Shortcut task not found.", [$"No shortcut task found with id: {taskId}"]), tasks.ScopeGeneration)
            : (task, null, tasks.ScopeGeneration);
    }

    private static void ApplyOptions(ShortcutTask task, ShortcutCommand options)
    {
        if (options.Speed is not null)
        {
            task.PlaybackSpeed = options.Speed.Value;
        }

        if (options.Loop is not null)
        {
            task.LoopEnabled = options.Loop.Value;
        }

        if (options.RepeatCount is not null)
        {
            task.RepeatCount = options.RepeatCount.Value;
        }

        if (options.RepeatDelayMs is not null)
        {
            task.RepeatDelayMs = options.RepeatDelayMs.Value;
        }

        if (options.RepeatDelayMinMs is not null && options.RepeatDelayMaxMs is not null)
        {
            task.UseRandomRepeatDelay = true;
            task.RepeatDelayMinMs = options.RepeatDelayMinMs.Value;
            task.RepeatDelayMaxMs = options.RepeatDelayMaxMs.Value;
        }

        if (options.RunWhileHeld)
        {
            task.RunWhileHeld = true;
        }

        if (options.ClearWindowRules)
        {
            task.WindowRules.Clear();
        }

        if (options.WindowRules is not null)
        {
            task.WindowRules.Clear();
            foreach (var rule in options.WindowRules.Where(rule => rule is not null))
            {
                task.WindowRules.Add(new ShortcutWindowRule
                {
                    Field = rule.Field,
                    MatchMode = rule.MatchMode,
                    Value = rule.Value,
                });
            }
        }
    }



}
