namespace CrossMacro.Cli.Serialization;

public sealed record QuickSetupCommandData(
    string Provider,
    bool Applicable,
    bool Applied);
