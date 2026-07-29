namespace CrossMacro.Cli.Options;

public sealed record SettingsListKeysCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
