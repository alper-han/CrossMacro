namespace CrossMacro.Cli.Services.WindowManagement;

public sealed record WindowInfoData(
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
