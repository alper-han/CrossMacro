namespace CrossMacro.Cli;

/// <summary>
/// Stable machine-readable metadata for one top-level CrossMacro CLI command.
/// </summary>
public sealed record CliCommandContract(
    string CommandToken,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Subcommands,
    IReadOnlyList<CliOptionContract> Options);
