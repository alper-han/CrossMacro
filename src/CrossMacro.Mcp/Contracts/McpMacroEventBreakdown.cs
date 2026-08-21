namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Count of each supported persisted macro event kind.
/// </summary>
public sealed record McpMacroEventBreakdown(
    int MouseMove,
    int ButtonPress,
    int ButtonRelease,
    int Click,
    int KeyPress,
    int KeyRelease);
