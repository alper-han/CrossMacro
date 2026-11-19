namespace CrossMacro.Commands.Options;

public sealed record WindowOptions(
    WindowAction Action,
    WindowSelector? Selector = null,
    int? X = null,
    int? Y = null,
    int? Width = null,
    int? Height = null,
    int? TimeoutMs = null,
    string? WorkspaceName = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CommandOptions(JsonOutput, LogLevel);
