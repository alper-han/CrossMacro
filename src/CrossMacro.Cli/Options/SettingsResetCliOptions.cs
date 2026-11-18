namespace CrossMacro.Cli.Options;

public sealed record SettingsResetCliOptions(string Key, bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedCliOptions(JsonOutput, LogLevel);
