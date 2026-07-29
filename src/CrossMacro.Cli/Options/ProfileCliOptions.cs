namespace CrossMacro.Cli.Options;

public sealed record ProfileCliOptions(
    ProfileCliAction Action,
    string? ProfileIdentifier = null,
    string? NewName = null,
    bool Force = false,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
