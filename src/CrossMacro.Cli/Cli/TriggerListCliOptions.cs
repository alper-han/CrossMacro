namespace CrossMacro.Cli;

public sealed record TriggerListCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
