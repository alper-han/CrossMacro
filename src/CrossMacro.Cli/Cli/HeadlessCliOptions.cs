namespace CrossMacro.Cli;

public sealed record HeadlessCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
