
namespace CrossMacro.Cli;

public sealed record TriggerCliOptions(
    TriggerCliAction Action,
    string? TaskId = null,
    string? Name = null,
    TriggerField? Field = null,
    TriggerMatchMode? MatchMode = null,
    string? Value = null,
    TriggerAction? TriggerActionVal = null,
    string? TargetProfileId = null,
    string? MacroFilePath = null,
    TriggerFireMode? FireMode = null,
    int? CooldownMs = null,
    int? DebounceMs = null,
    bool? Enabled = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
