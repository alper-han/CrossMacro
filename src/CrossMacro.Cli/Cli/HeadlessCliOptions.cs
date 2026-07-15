namespace CrossMacro.Cli;

public sealed record class HeadlessCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
