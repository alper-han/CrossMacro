
namespace CrossMacro.Application.Runtime;

public sealed record RunExecutionRequest(
    IReadOnlyList<RunScriptInputStep> Steps,
    double SpeedMultiplier = 1.0,
    int CountdownSeconds = 0,
    bool DryRun = false,
    IReadOnlyDictionary<string, string>? ImageAssets = null);
