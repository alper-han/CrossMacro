namespace CrossMacro.Cli.Options;

public sealed record TriggerListCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedCliOptions(JsonOutput, LogLevel);
