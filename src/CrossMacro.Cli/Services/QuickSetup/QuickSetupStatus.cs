namespace CrossMacro.Cli.Services.QuickSetup;

public readonly record struct QuickSetupStatus(
    bool Applicable,
    string Provider,
    bool ShouldPrompt);
