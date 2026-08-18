namespace CrossMacro.Cli.Options;

/// <summary>
/// Represents a single, top-level input primitive. The step is intentionally
/// kept in the same script grammar used by <c>run</c> so both entry points use
/// the same compiler, validation, coordinate handling, and cancellation path.
/// </summary>
public sealed record InputCliOptions(
    string Step,
    bool DryRun = false,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
