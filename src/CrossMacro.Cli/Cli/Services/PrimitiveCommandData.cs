using System.Collections.Generic;

namespace CrossMacro.Cli.Services;

public sealed record ClipboardTextData(string Value);

public sealed record ClipboardSetData(int Length, string Source);

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

public sealed record WindowListData(IReadOnlyList<WindowInfoData> Windows, int Count);

public sealed record WindowWaitData(bool Found, WindowInfoData? Window, int TimeoutMs);

public sealed record WindowMutationData(string Operation, bool Result);

public sealed record WorkspaceData(string? Workspace);

public sealed record ScreenPixelData(int X, int Y, string Color, string ProviderName, bool Relative);

public sealed record ScreenWaitColorData(int X, int Y, string ExpectedColor, string ActualColor, string ProviderName, bool Matched, int? TimeoutMs);

public sealed record ScreenSearchColorData(
    bool Found,
    int? X,
    int? Y,
    string? Color,
    string ExpectedColor,
    int RegionX,
    int RegionY,
    int RegionWidth,
    int RegionHeight,
    int Tolerance,
    string ProviderName);
