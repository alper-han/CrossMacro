namespace CrossMacro.Cli;

public sealed record class SettingsGetCliOptions(string? Key = null, bool JsonOutput = false, string? LogLevel = null, bool All = false)
    : CliCommandOptions(JsonOutput, LogLevel);
