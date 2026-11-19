namespace CrossMacro.Commands.Options;

public sealed record SettingsSetOptions(string Key, string Value, bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedOptions(JsonOutput, LogLevel);
