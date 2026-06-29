namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Describes a single desktop window returned by the window manager.
/// </summary>
public sealed record WindowInfo
{
    /// <summary>Compositor-assigned address, e.g. "0x55a1b2c3d4e5".</summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>Window title (WM_NAME / _NET_WM_NAME).</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Window class (WM_CLASS instance or app-id on Wayland).</summary>
    public string Class { get; init; } = string.Empty;

    /// <summary>Process ID of the window owner, or -1 if unknown.</summary>
    public int Pid { get; init; } = -1;

    /// <summary>Workspace name or ID, or empty string if unknown.</summary>
    public string Workspace { get; init; } = string.Empty;

    /// <summary>Whether this is the currently focused window.</summary>
    public bool IsFocused { get; init; }

    /// <summary>Whether the window is in fullscreen mode.</summary>
    public bool IsFullscreen { get; init; }

    /// <summary>Whether the window is maximized.</summary>
    public bool IsMaximized { get; init; }

    /// <summary>Whether the window is floating (not tiled).</summary>
    public bool IsFloating { get; init; }

    /// <summary>Whether the window is pinned to all workspaces.</summary>
    public bool IsPinned { get; init; }

    /// <summary>Whether the window is hidden/minimized.</summary>
    public bool IsHidden { get; init; }

    /// <summary>Window's X coordinate on screen.</summary>
    public int X { get; init; }

    /// <summary>Window's Y coordinate on screen.</summary>
    public int Y { get; init; }

    /// <summary>Window's width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Window's height in pixels.</summary>
    public int Height { get; init; }
}
