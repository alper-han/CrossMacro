namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// The shared text and error envelope returned by CrossMacro MCP tools.
/// Tool-specific structured data is defined by each tool's generated DTO.
/// </summary>
public sealed record McpToolOutcome(
    bool Success,
    int ExitCode,
    string Message,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<McpToolError> Errors);
