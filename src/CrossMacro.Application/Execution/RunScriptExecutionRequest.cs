
namespace CrossMacro.Application.Execution;

public sealed class RunScriptExecutionRequest
{
    public IReadOnlyList<string> Steps { get; init; } = [];
    public string? StepFilePath { get; init; }
    public double SpeedMultiplier { get; init; } = 1.0;
    public int CountdownSeconds { get; init; }
    public bool DryRun { get; init; }
    public IReadOnlyList<RunImageAssetRequest> ImageAssets { get; init; } = [];
}
