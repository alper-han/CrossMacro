namespace CrossMacro.Commands.Options;

public sealed record ShortcutListOptions(bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedOptions(JsonOutput, LogLevel);
