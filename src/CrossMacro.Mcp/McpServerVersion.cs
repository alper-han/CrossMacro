using System.Reflection;

namespace CrossMacro.Mcp;

/// <summary>The one product version reported by the MCP server handshake and by system.status.</summary>
internal static class McpServerVersion
{
    public static string Current { get; } = (Assembly.GetEntryAssembly() ?? typeof(McpServerVersion).Assembly)
        .GetName().Version?.ToString() ?? "unknown";
}
