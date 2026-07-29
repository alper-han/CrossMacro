
namespace CrossMacro.Cli.Options;

public sealed record TextExpansionCliOptions(
    TextExpansionCliAction Action,
    string? Trigger = null,
    string? Replacement = null,
    PasteMethod Method = PasteMethod.CtrlV,
    TextInsertionMode InsertionMode = TextInsertionMode.Paste,
    DirectTypingMethod DirectTypingMethod = DirectTypingMethod.FastBatch,
    string? ProfileIdentifier = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
