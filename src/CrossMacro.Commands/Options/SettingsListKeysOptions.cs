namespace CrossMacro.Commands.Options;

public sealed record SettingsListKeysOptions(bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedOptions(JsonOutput, LogLevel);
