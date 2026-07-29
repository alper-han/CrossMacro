
namespace CrossMacro.Cli.Services;

public sealed class TriggerCliService(IManageTrigger manageTrigger) : ITriggerCliService
{
    private readonly IManageTrigger _manageTrigger = manageTrigger;
    private IReadOnlyList<TriggerTask> _tasks = [];

    public async Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.ListTasksAsync(
            taskKind: "trigger",
            loadAsync: async () => _tasks = (await _manageTrigger.ListAsync(cancellationToken).ConfigureAwait(false)).Tasks,
            getTasks: () => _tasks,
            mapTask: MapTask,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<CliCommandExecutionResult> ExecuteAsync(TriggerCliOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Action switch
        {
            TriggerCliAction.Add => await AddAsync(options, cancellationToken).ConfigureAwait(false),
            TriggerCliAction.Edit => await EditAsync(options, cancellationToken).ConfigureAwait(false),
            TriggerCliAction.Remove => await RemoveAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false),
            TriggerCliAction.Enable => await SetEnabledAsync(options.TaskId ?? string.Empty, enabled: true, cancellationToken).ConfigureAwait(false),
            TriggerCliAction.Disable => await SetEnabledAsync(options.TaskId ?? string.Empty, enabled: false, cancellationToken).ConfigureAwait(false),
            _ => CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Unknown trigger action."),
        };
    }

    private async Task<CliCommandExecutionResult> AddAsync(TriggerCliOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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

        _ = await _manageTrigger.AddAsync(task, cancellationToken).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Trigger task added: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> EditAsync(TriggerCliOptions options, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
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
        _ = await _manageTrigger.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Trigger task updated: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> RemoveAsync(string taskId, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
        cancellationToken.ThrowIfCancellationRequested();
        _ = await _manageTrigger.RemoveAsync(new TaskRequest(task.Id), cancellationToken).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Trigger task removed: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> SetEnabledAsync(string taskId, bool enabled, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
        if (enabled && !task.CanBeEnabled)
        {
            return CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "Trigger task cannot be enabled.",
                ["Trigger task requires valid action configurations (Target Profile ID or Macro Path) before enabling."]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _ = await _manageTrigger.SetEnabledAsync(new TaskRequest(task.Id, enabled), cancellationToken).ConfigureAwait(false);
        _ = task.TrySetEnabled(enabled);
        var verb = enabled ? "enabled" : "disabled";
        return CliCommandExecutionResult.Ok($"Trigger task {verb}: {task.Name}.", MapTask(task));
    }

    private async Task<(TriggerTask? Task, CliCommandExecutionResult? Result)> LoadAndFindAsync(string taskId, CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.FindTaskAsync(
            taskId,
            "trigger",
            "Trigger",
            async () => _tasks = (await _manageTrigger.ListAsync(cancellationToken).ConfigureAwait(false)).Tasks,
            task => task.Id,
            cancellationToken).ConfigureAwait(false);
    }

    private static TriggerTaskData MapTask(TriggerTask task)
    {
        return new TriggerTaskData(
            task.Id,
            task.Name,
            task.IsEnabled,
            task.Field.ToString(),
            task.MatchMode.ToString(),
            task.Value,
            task.Action.ToString(),
            task.Action is TriggerOperation.SwitchProfile ? task.TargetProfileId : null,
            task.Action is TriggerOperation.RunMacro ? task.MacroFilePath : null,
            task.FireMode.ToString(),
            task.CooldownMs,
            task.DebounceMs,
            task.LastTriggeredTime,
            task.LastStatus);
    }
}
