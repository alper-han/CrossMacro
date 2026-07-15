namespace CrossMacro.Cli;

public sealed record class ShortcutListCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
