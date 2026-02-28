namespace CrossMacro.Cli.Services;

public sealed class MacroExecutionRequest
{
    public required string MacroFilePath { get; init; }

    public double SpeedMultiplier { get; init; } = 1.0;

    public bool Loop { get; init; }

    public int RepeatCount { get; init; } = 1;

    public int RepeatDelayMs { get; init; }

    public int CountdownSeconds { get; init; }

    public bool DryRun { get; init; }
}
