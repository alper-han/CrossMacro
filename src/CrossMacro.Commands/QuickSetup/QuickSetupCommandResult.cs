namespace CrossMacro.Commands.QuickSetup;

public readonly record struct QuickSetupCommandResult(
    bool Applicable,
    string Provider,
    QuickSetupResult Result);
