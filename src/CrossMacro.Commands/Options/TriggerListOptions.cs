namespace CrossMacro.Commands.Options;

public sealed record TriggerListOptions(bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedOptions(JsonOutput, LogLevel);
