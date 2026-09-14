namespace CrossMacro.Cli.Services.QuickSetup;

public readonly record struct QuickSetupCliResult(
    bool Applicable,
    string Provider,
    QuickSetupResult Result);
