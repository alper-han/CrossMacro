namespace CrossMacro.Cli;

public sealed record class SettingsResetCliOptions(string Key, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
