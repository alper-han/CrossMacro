namespace CrossMacro.Cli;

public sealed record class ClipboardCliOptions(
    ClipboardCliAction Action,
    string? Text = null,
    string? FilePath = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
