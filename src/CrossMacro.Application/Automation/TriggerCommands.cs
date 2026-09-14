using System.Globalization;

namespace CrossMacro.Application.Automation;

public sealed class TriggerCommands(IManageTrigger manageTrigger) : ITriggerCommands
{
    private readonly IManageTrigger _manageTrigger = manageTrigger;

    public async Task<TaskCommandResult<TriggerTask>> ListAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _manageTrigger.ListAsync(cancellationToken).ConfigureAwait(false);
            return new(Success: true, $"Loaded {result.Tasks.Count} trigger task(s).", [], Tasks: result.Tasks);
        }
        catch (TaskScopeUnavailableException ex)
        {
            return TaskCommandResult.Fail<TriggerTask>(ex.Message);
        }
    }

    public async Task<TaskCommandResult<TriggerTask>> ExecuteAsync(TriggerCommand options, CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteCoreAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is TaskScopeConflictException or TaskScopeUnavailableException)
        {
            return TaskCommandResult.Fail<TriggerTask>(ex.Message);
        }
    }

    private async Task<TaskCommandResult<TriggerTask>> ExecuteCoreAsync(TriggerCommand options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Action switch
        {
            TriggerCommandAction.Add => await AddAsync(options, cancellationToken).ConfigureAwait(false),
            TriggerCommandAction.Edit => await EditAsync(options, cancellationToken).ConfigureAwait(false),
            TriggerCommandAction.Remove => await RemoveAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false),
            TriggerCommandAction.Enable => await SetEnabledAsync(options.TaskId ?? string.Empty, enabled: true, cancellationToken).ConfigureAwait(false),
            TriggerCommandAction.Disable => await SetEnabledAsync(options.TaskId ?? string.Empty, enabled: false, cancellationToken).ConfigureAwait(false),
            _ => TaskCommandResult.Fail<TriggerTask>("Unknown trigger action."),
        };
    }

    private async Task<TaskCommandResult<TriggerTask>> AddAsync(TriggerCommand options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scope = await _manageTrigger.ListAsync(cancellationToken).ConfigureAwait(false);
        var task = new TriggerTask
        {
            Name = options.Name ?? string.Empty,
            Field = options.Field ?? TriggerField.None,
            MatchMode = options.MatchMode ?? TriggerMatchMode.Equals,
            Value = options.Value ?? string.Empty,
            Action = options.TriggerActionVal ?? TriggerOperation.SwitchProfile,
            TargetProfileId = options.TargetProfileId ?? string.Empty,
            MacroFilePath = options.MacroFilePath ?? string.Empty,
            FireMode = options.FireMode ?? TriggerFireMode.OnceOnChange,
            CooldownMs = options.CooldownMs,
            DebounceMs = options.DebounceMs,
        };

        if (options.Enabled is not null)
        {
            _ = task.TrySetEnabled(options.Enabled.Value);
        }

        task = await _manageTrigger.AddAsync(task, scope.ScopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<TriggerTask>($"Trigger task added: {task.Name}.", task);
    }

    private async Task<TaskCommandResult<TriggerTask>> EditAsync(TriggerCommand options, CancellationToken cancellationToken)
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

        if (options.Field is not null)
        {
            task.Field = options.Field.Value;
        }

        if (options.MatchMode is not null)
        {
            task.MatchMode = options.MatchMode.Value;
        }

        if (options.Value is not null)
        {
            task.Value = options.Value;
        }

        if (options.TriggerActionVal is not null)
        {
            task.Action = options.TriggerActionVal.Value;
        }

        if (options.TargetProfileId is not null)
        {
            task.TargetProfileId = options.TargetProfileId;
        }

        if (options.MacroFilePath is not null)
        {
            task.MacroFilePath = options.MacroFilePath;
        }

        if (options.FireMode is not null)
        {
            task.FireMode = options.FireMode.Value;
        }

        if (options.CooldownMs is not null)
        {
            task.CooldownMs = options.CooldownMs.Value is 0 ? null : options.CooldownMs.Value;
        }

        if (options.DebounceMs is not null)
        {
            task.DebounceMs = options.DebounceMs.Value is 0 ? null : options.DebounceMs.Value;
        }

        if (options.Enabled is not null)
        {
            _ = task.TrySetEnabled(options.Enabled.Value);
        }

        cancellationToken.ThrowIfCancellationRequested();
        task = await _manageTrigger.UpdateAsync(task, parsed.ScopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<TriggerTask>($"Trigger task updated: {task.Name}.", task);
    }

    private async Task<TaskCommandResult<TriggerTask>> RemoveAsync(string taskId, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
        cancellationToken.ThrowIfCancellationRequested();
        _ = await _manageTrigger.RemoveAsync(new TaskRequest(task.Id, ExpectedScopeGeneration: parsed.ScopeGeneration), cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<TriggerTask>($"Trigger task removed: {task.Name}.", task);
    }

    private async Task<TaskCommandResult<TriggerTask>> SetEnabledAsync(string taskId, bool enabled, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
        if (enabled && !task.CanBeEnabled)
        {
            return TaskCommandResult.Fail<TriggerTask>(
                "Trigger task cannot be enabled.",
                ["Trigger task requires valid action configurations (Target Profile ID or Macro Path) before enabling."]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _ = await _manageTrigger.SetEnabledAsync(new TaskRequest(task.Id, enabled, parsed.ScopeGeneration), cancellationToken).ConfigureAwait(false);
        _ = task.TrySetEnabled(enabled);
        var verb = enabled ? "enabled" : "disabled";
        return TaskCommandResult.Ok<TriggerTask>($"Trigger task {verb}: {task.Name}.", task);
    }

    private async Task<(TriggerTask? Task, TaskCommandResult<TriggerTask>? Result, long ScopeGeneration)> LoadAndFindAsync(string taskId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParse(taskId, out var id))
        {
            return (null, TaskCommandResult.Fail<TriggerTask>("Invalid trigger task id format.", [$"Task id is not a valid GUID: {taskId}"]), 0);
        }
        var tasks = await _manageTrigger.ListAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var task = tasks.Tasks.FirstOrDefault(candidate => candidate.Id == id);
        return task is null
            ? (null, TaskCommandResult.Fail<TriggerTask>("Trigger task not found.", [$"No trigger task found with id: {taskId}"]), tasks.ScopeGeneration)
            : (task, null, tasks.ScopeGeneration);
    }



}
