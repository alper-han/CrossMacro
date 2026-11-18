namespace CrossMacro.Mcp.Tests;

internal static class McpToolTypeCatalog
{
    public static IReadOnlyList<Type> All { get; } =
    [
        typeof(McpRuntimeTools),
        typeof(McpSettingsTools),
        typeof(McpProfileTools),
        typeof(McpTextExpansionTools),
        typeof(McpTaskTools),
        typeof(McpAutomationTools),
        typeof(McpCommandTools),
        typeof(McpMacroTools),
        typeof(McpClipboardTools),
        typeof(McpWindowTools),
        typeof(McpScreenTools),
    ];
}
