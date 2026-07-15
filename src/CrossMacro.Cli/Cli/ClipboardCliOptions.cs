namespace CrossMacro.Cli;

public sealed record ClipboardCliOptions(
    ClipboardCliAction Action,
    string? Text = null,
    string? FilePath = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
