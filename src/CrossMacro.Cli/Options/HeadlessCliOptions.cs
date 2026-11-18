namespace CrossMacro.Cli.Options;

public sealed record HeadlessCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedCliOptions(JsonOutput, LogLevel);
