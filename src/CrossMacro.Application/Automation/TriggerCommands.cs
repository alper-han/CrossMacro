namespace CrossMacro.Application.Automation;

public sealed class TriggerCommands(IManageTrigger manageTrigger) : TaskCommands<TriggerTask>(manageTrigger), ITriggerCommands
{
    private readonly IManageTrigger _manageTrigger = manageTrigger;

    protected override string Kind => "trigger";

    protected override string CannotEnableReason => "Trigger task requires valid action configurations (Target Profile ID or Macro Path) before enabling.";

    protected override Guid IdOf(TriggerTask task) => task.Id;

    protected override string NameOf(TriggerTask task) => task.Name;

    protected override bool CanBeEnabled(TriggerTask task) => task.CanBeEnabled;

    public Task<TaskCommandResult<TriggerTask>> ExecuteAsync(TriggerCommand options, CancellationToken cancellationToken) =>
        GuardAsync(() => ExecuteCoreAsync(options, cancellationToken));

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
            Action = options.Operation ?? TriggerOperation.SwitchProfile,
            TargetProfileId = options.TargetProfileId ?? string.Empty,
            MacroFilePath = options.MacroFilePath ?? string.Empty,
            FireMode = options.FireMode ?? TriggerFireMode.OnceOnChange,
            CooldownMs = options.CooldownMs,
            DebounceMs = options.DebounceMs,
        };

        if (options.Enabled is not null && !task.TrySetEnabled(options.Enabled.Value))
        {
            return CannotEnable();
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

        if (options.Operation is not null)
        {
            task.Action = options.Operation.Value;
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

        if (options.Enabled is not null && !task.TrySetEnabled(options.Enabled.Value))
        {
            return CannotEnable();
        }

        cancellationToken.ThrowIfCancellationRequested();
        task = await _manageTrigger.UpdateAsync(task, parsed.ScopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<TriggerTask>($"Trigger task updated: {task.Name}.", task);
    }
}
