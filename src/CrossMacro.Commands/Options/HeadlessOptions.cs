namespace CrossMacro.Commands.Options;

public sealed record HeadlessOptions(bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedOptions(JsonOutput, LogLevel);
