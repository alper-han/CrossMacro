using CrossMacro.Cli;

namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Stable MCP error codes corresponding to CLI exit categories.
/// </summary>
public static class McpCliErrorCodeCatalog
{
    public static IReadOnlyDictionary<CliExitCode, string> ByExitCode { get; } =
        new Dictionary<CliExitCode, string>
        {
            [CliExitCode.Success] = "success",
            [CliExitCode.InvalidArguments] = "invalid_arguments",
            [CliExitCode.FileError] = "file_error",
            [CliExitCode.ValidationError] = "validation_error",
            [CliExitCode.EnvironmentError] = "environment_error",
            [CliExitCode.RuntimeError] = "runtime_error",
            [CliExitCode.Cancelled] = "cancelled",
        };

    public static string GetCode(int exitCode) =>
        ByExitCode.TryGetValue((CliExitCode)exitCode, out var code)
            ? code
            : "runtime_error";
}
