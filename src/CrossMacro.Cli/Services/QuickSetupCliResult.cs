namespace CrossMacro.Cli.Services;

public readonly record struct QuickSetupCliResult(
    bool Applicable,
    string Provider,
    QuickSetupResult Result);
