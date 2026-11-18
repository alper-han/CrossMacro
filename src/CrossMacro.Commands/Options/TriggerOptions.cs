using CrossMacro.Core.Models.Automation.Triggers;

namespace CrossMacro.Commands.Options;

public sealed record TriggerOptions(
    TriggerTaskAction Action,
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
    bool? Enabled = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : ProfileBackedOptions(JsonOutput, LogLevel);
