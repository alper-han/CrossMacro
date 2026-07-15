namespace CrossMacro.Cli.Services;

public sealed record TextExpansionData(
    string Trigger,
    string Replacement,
    bool IsEnabled,
    string Method,
    string InsertionMode,
    string DirectTypingMethod);
