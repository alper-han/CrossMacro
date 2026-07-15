namespace CrossMacro.Cli;

public sealed record class WindowCliOptions(
    WindowCliAction Action,
    WindowSelector? Selector = null,
    int? X = null,
    int? Y = null,
    int? Width = null,
    int? Height = null,
    int? TimeoutMs = null,
    string? WorkspaceName = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
