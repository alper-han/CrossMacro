namespace CrossMacro.Cli.Services;

public sealed record class TextExpansionData(
    string Trigger,
    string Replacement,
    bool IsEnabled,
    string Method,
    string InsertionMode,
    string DirectTypingMethod);
