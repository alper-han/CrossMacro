namespace CrossMacro.Cli.Services;

internal sealed class RunImageAssetLoadOutcome
{
    private RunImageAssetLoadOutcome()
    {
    }

    public bool Success { get; private init; }
    public IReadOnlyDictionary<string, string>? Images { get; private init; }
    public MacroExecutionResult? ErrorResult { get; private init; }

    public static RunImageAssetLoadOutcome Ok(IReadOnlyDictionary<string, string>? images)
    {
        return new RunImageAssetLoadOutcome
        {
            Success = true,
            Images = images,
        };
    }

    public static RunImageAssetLoadOutcome Fail(MacroExecutionResult errorResult)
    {
        return new RunImageAssetLoadOutcome
        {
            Success = false,
            ErrorResult = errorResult,
        };
    }
}
