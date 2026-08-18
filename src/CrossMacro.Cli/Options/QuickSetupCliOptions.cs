namespace CrossMacro.Cli.Options;

public sealed record QuickSetupCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
