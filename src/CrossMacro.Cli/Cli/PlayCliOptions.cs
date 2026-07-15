namespace CrossMacro.Cli;

public sealed record class PlayCliOptions(
    string MacroFilePath,
    double SpeedMultiplier = 1.0,
    bool Loop = false,
    int RepeatCount = 1,
    int RepeatDelayMs = 0,
    int CountdownSeconds = 0,
    int TimeoutSeconds = 0,
    bool DryRun = false,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
