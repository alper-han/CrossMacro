namespace CrossMacro.Cli;

public sealed record class SettingsSetCliOptions(string Key, string Value, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
