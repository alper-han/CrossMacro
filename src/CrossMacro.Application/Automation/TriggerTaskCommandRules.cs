namespace CrossMacro.Application.Automation;

public sealed class TriggerTaskCommandRules : TaskCommandRules<TriggerTask, TriggerCommand>
{
    public static TriggerTaskCommandRules Instance { get; } = new();

    private TriggerTaskCommandRules()
    {
    }

    public override TaskCommandResult<TriggerTask>? ApplyAdd(TriggerTask task, TriggerCommand command)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(command);
        task.Name = command.Name ?? string.Empty;
        task.Field = command.Field ?? TriggerField.None;
        task.MatchMode = command.MatchMode ?? TriggerMatchMode.Equals;
        task.Value = command.Value ?? string.Empty;
        task.Action = command.Operation ?? TriggerOperation.SwitchProfile;
        task.TargetProfileId = command.TargetProfileId ?? string.Empty;
        task.MacroFilePath = command.MacroFilePath ?? string.Empty;
        task.FireMode = command.FireMode ?? TriggerFireMode.OnceOnChange;
        task.CooldownMs = command.CooldownMs;
        task.DebounceMs = command.DebounceMs;
        return null;
    }

    public override TaskCommandResult<TriggerTask>? ApplyEdit(TriggerTask task, TriggerCommand command)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(command);
        if (!string.IsNullOrWhiteSpace(command.Name))
        {
            task.Name = command.Name;
        }

        if (command.Field is not null)
        {
            task.Field = command.Field.Value;
        }

        if (command.MatchMode is not null)
        {
            task.MatchMode = command.MatchMode.Value;
        }

        if (command.Value is not null)
        {
            task.Value = command.Value;
        }

        if (command.Operation is not null)
        {
            task.Action = command.Operation.Value;
        }

        if (command.TargetProfileId is not null)
        {
            task.TargetProfileId = command.TargetProfileId;
        }

        if (command.MacroFilePath is not null)
        {
            task.MacroFilePath = command.MacroFilePath;
        }

        if (command.FireMode is not null)
        {
            task.FireMode = command.FireMode.Value;
        }

        if (command.CooldownMs is not null)
        {
            task.CooldownMs = command.CooldownMs.Value is 0 ? null : command.CooldownMs.Value;
        }

        if (command.DebounceMs is not null)
        {
            task.DebounceMs = command.DebounceMs.Value is 0 ? null : command.DebounceMs.Value;
        }

        return null;
    }
}
