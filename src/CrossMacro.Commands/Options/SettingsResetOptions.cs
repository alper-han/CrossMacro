namespace CrossMacro.Commands.Options;

public sealed record SettingsResetOptions(string Key, bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedOptions(JsonOutput, LogLevel);
