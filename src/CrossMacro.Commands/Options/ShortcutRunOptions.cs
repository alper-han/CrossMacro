namespace CrossMacro.Commands.Options;

public sealed record ShortcutRunOptions(string TaskId, bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedOptions(JsonOutput, LogLevel);
