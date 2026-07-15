namespace CrossMacro.Cli;

public sealed record SettingsListKeysCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
