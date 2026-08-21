namespace CrossMacro.Mcp.Services;

/// <summary>
/// Validates the small structured command surface exposed through MCP.
/// </summary>
public interface IMcpCommandPolicy
{
    public McpToolOutcome Validate(string command, IReadOnlyList<string> arguments);
}
