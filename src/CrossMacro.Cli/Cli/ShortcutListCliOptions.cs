namespace CrossMacro.Cli;

public sealed record ShortcutListCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
