namespace CrossMacro.Cli;

public sealed record class ProfileCliOptions(
    ProfileCliAction Action,
    string? ProfileIdentifier = null,
    string? NewName = null,
    bool Force = false,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
