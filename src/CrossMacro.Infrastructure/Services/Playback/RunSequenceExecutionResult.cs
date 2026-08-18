
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunSequenceExecutionResult
{
    private RunSequenceExecutionResult()
    {
    }

    public bool Success { get; private init; }
    public bool IsCancelled { get; private init; }
    public bool IsAbsolutePlaybackUnsupported { get; private init; }
    public bool IsInputInjectionPermissionRequired { get; private init; }
    public string? ErrorMessage { get; private init; }
    public IReadOnlyDictionary<string, string> RuntimeVariables { get; private init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public static RunSequenceExecutionResult Succeeded(IReadOnlyDictionary<string, string>? runtimeVariables = null)
    {
        return new RunSequenceExecutionResult
        {
            Success = true,
            RuntimeVariables = runtimeVariables is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(runtimeVariables, StringComparer.OrdinalIgnoreCase),
        };
    }

    public static RunSequenceExecutionResult Cancelled() => new() { Success = false, IsCancelled = true };

    public static RunSequenceExecutionResult AbsolutePlaybackUnsupported(string errorMessage) => new()
    {
        Success = false,
        IsCancelled = false,
        IsAbsolutePlaybackUnsupported = true,
        ErrorMessage = errorMessage,
    };

    public static RunSequenceExecutionResult InputInjectionPermissionRequired(string errorMessage) => new()
    {
        Success = false,
        IsCancelled = false,
        IsInputInjectionPermissionRequired = true,
        ErrorMessage = errorMessage,
    };

    public static RunSequenceExecutionResult Failed(string errorMessage) => new()
    {
        Success = false,
        IsCancelled = false,
        ErrorMessage = errorMessage,
    };
}
