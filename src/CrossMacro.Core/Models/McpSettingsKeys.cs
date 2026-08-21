namespace CrossMacro.Core.Models;

/// <summary>
/// Stable settings CLI keys for MCP settings that are not capability switches.
/// </summary>
public static class McpSettingsKeys
{
    public const string Prefix = "mcp.";
    public const string ApprovalTimeoutSeconds = "mcp.approvalTimeoutSeconds";
    public const string MacroReadRoots = "mcp.paths.macroRead";
    public const string MacroWriteRoots = "mcp.paths.macroWrite";
    public const string ImageReadRoots = "mcp.paths.imageRead";
    public const string ImageWriteRoots = "mcp.paths.imageWrite";
    public const string FileReadRoots = "mcp.paths.fileRead";
    public const string FileWriteRoots = "mcp.paths.fileWrite";

    /// <summary>
    /// MCP settings define the policy that constrains an MCP session. They must
    /// be managed by a local UI or CLI session, not by the MCP client itself.
    /// </summary>
    public static bool IsPolicyKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
