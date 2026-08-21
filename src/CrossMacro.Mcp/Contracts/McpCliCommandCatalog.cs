namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// CLI command families currently exposed through the MCP compatibility tool.
/// This is intentionally narrower than the full CLI contract until each family
/// has an MCP adapter and matching authorization behavior.
/// </summary>
public static class McpCliCommandCatalog
{
    public static IReadOnlyList<string> SupportedCommands { get; } = Array.AsReadOnly(
    [
        "macro",
        "play",
        "doctor",
        "settings",
        "profile",
        "text-expansion",
        "text",
        "schedule",
        "shortcut",
        "trigger",
        "record",
        "run",
        "move",
        "click",
        "down",
        "up",
        "scroll",
        "key",
        "tap",
        "type",
        "delay",
        "clipboard",
        "window",
        "screen",
        "screenshot",
    ]);
}
