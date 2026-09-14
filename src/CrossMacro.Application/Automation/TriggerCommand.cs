
namespace CrossMacro.Application.Automation;

public sealed record TriggerCommand(
    TriggerCommandAction Action,
    string? TaskId = null,
    string? Name = null,
    TriggerField? Field = null,
    TriggerMatchMode? MatchMode = null,
    string? Value = null,
    TriggerOperation? TriggerActionVal = null,
    string? TargetProfileId = null,
    string? MacroFilePath = null,
    TriggerFireMode? FireMode = null,
    int? CooldownMs = null,
    int? DebounceMs = null,
    bool? Enabled = null);
