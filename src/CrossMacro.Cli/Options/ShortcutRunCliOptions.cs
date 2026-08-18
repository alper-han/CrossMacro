namespace CrossMacro.Cli.Options;

public sealed record ShortcutRunCliOptions(string TaskId, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
