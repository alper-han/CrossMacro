namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Window metadata surfaced by the existing CLI window-query adapter.
/// </summary>
public sealed record McpWindowInfo(
    string Address,
    string Title,
    string Class,
    int Pid,
    string Workspace,
    bool IsFocused,
    bool IsFullscreen,
    bool IsMaximized,
    bool IsFloating,
    bool IsPinned,
    bool IsHidden,
    int X,
    int Y,
    int Width,
    int Height);
