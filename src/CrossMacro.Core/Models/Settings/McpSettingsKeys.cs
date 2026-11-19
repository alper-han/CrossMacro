namespace CrossMacro.Core.Models.Settings;

/// <summary>
/// Stable settings CLI keys for MCP settings that are not capability switches.
/// </summary>
public static class McpSettingsKeys
{
    public const string Prefix = "mcp.";
    public const string MacroReadRoots = "mcp.paths.macroRead";
    public const string MacroWriteRoots = "mcp.paths.macroWrite";
    public const string ImageReadRoots = "mcp.paths.imageRead";
    public const string ImageWriteRoots = "mcp.paths.imageWrite";
    public const string FileReadRoots = "mcp.paths.fileRead";
    public const string FileWriteRoots = "mcp.paths.fileWrite";

    public static string GetRootsKey(McpPathSetting setting) => setting switch
    {
        McpPathSetting.MacroRead => MacroReadRoots,
        McpPathSetting.MacroWrite => MacroWriteRoots,
        McpPathSetting.ImageRead => ImageReadRoots,
        McpPathSetting.ImageWrite => ImageWriteRoots,
        McpPathSetting.FileRead => FileReadRoots,
        McpPathSetting.FileWrite => FileWriteRoots,
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "Unknown MCP path setting."),
    };

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
