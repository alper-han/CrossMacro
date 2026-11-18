namespace CrossMacro.Commands.Options;

public sealed record ProfileOptions(
    ProfileAction Action,
    string? ProfileIdentifier = null,
    string? NewName = null,
    bool Force = false,
    bool JsonOutput = false,
    string? LogLevel = null)
    : ProfileBackedOptions(JsonOutput, LogLevel);
