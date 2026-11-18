using CrossMacro.Core.Models.Automation.TextExpansion;

namespace CrossMacro.Commands.Options;

public sealed record TextExpansionOptions(
    TextExpansionAction Action,
    string? Trigger = null,
    string? Replacement = null,
    PasteMethod Method = PasteMethod.CtrlV,
    TextInsertionMode InsertionMode = TextInsertionMode.Paste,
    DirectTypingMethod DirectTypingMethod = DirectTypingMethod.FastBatch,
    string? ProfileIdentifier = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : ProfileBackedOptions(JsonOutput, LogLevel);
