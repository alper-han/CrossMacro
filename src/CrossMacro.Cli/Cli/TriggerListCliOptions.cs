namespace CrossMacro.Cli;

public sealed record class TriggerListCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
