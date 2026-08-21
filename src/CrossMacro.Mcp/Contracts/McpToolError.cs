namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// A stable, machine-readable error reported by a CrossMacro MCP tool.
/// </summary>
public sealed record McpToolError(string Code, string Message);
