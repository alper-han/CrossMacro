namespace CrossMacro.Mcp.Services.Security;

/// <summary>
/// Validates the small structured command surface exposed through MCP.
/// </summary>
public interface IMcpCommandPolicy
{
    public McpToolOutcome Validate(string command, IReadOnlyList<string> arguments);
}
