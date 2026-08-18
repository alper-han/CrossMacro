namespace CrossMacro.Cli.Options;

public sealed record SettingsSetCliOptions(string Key, string Value, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
