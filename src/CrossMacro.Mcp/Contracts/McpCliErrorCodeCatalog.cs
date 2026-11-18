using System.Collections.Frozen;

namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Stable MCP error codes corresponding to CLI exit categories.
/// </summary>
public static class McpCliErrorCodeCatalog
{
    public static IReadOnlyDictionary<ExecutionOutcomeCode, string> ByExitCode { get; } =
        new Dictionary<ExecutionOutcomeCode, string>
        {
            [ExecutionOutcomeCode.Success] = "success",
            [ExecutionOutcomeCode.InvalidArguments] = "invalid_arguments",
            [ExecutionOutcomeCode.FileError] = "file_error",
            [ExecutionOutcomeCode.ValidationError] = "validation_error",
            [ExecutionOutcomeCode.EnvironmentError] = "environment_error",
            [ExecutionOutcomeCode.RuntimeError] = "runtime_error",
            [ExecutionOutcomeCode.Cancelled] = "cancelled",
        }.ToFrozenDictionary();

    public static string GetCode(int exitCode) =>
        ByExitCode.TryGetValue((ExecutionOutcomeCode)exitCode, out var code)
            ? code
            : "runtime_error";
}
