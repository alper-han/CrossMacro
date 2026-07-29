
namespace CrossMacro.Cli.Options;

public sealed record RunCliOptions(
    IReadOnlyList<string> Steps,
    string? StepFilePath = null,
    double SpeedMultiplier = 1.0,
    int CountdownSeconds = 0,
    int TimeoutSeconds = 0,
    bool DryRun = false,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
