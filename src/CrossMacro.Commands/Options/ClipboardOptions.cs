namespace CrossMacro.Commands.Options;

public sealed record ClipboardOptions(
    ClipboardAction Action,
    string? Text = null,
    string? FilePath = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CommandOptions(JsonOutput, LogLevel);
