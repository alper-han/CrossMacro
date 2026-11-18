namespace CrossMacro.Cli.Options;

public sealed record ShortcutListCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedCliOptions(JsonOutput, LogLevel);
