namespace CrossMacro.Cli;

public sealed record class SettingsListKeysCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
