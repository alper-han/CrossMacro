namespace CrossMacro.Cli.Services;

public readonly record struct QuickSetupStatus(
    bool Applicable,
    string Provider,
    bool ShouldPrompt);
