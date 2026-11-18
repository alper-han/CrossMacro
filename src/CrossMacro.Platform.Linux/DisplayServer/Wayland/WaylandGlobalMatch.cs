
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal enum WaylandGlobalMatch
{
    /// <summary>The interface is not used by this client.</summary>
    Unknown,

    /// <summary>The compositor advertises an older version than the client can work with.</summary>
    VersionTooOld,

    Bindable,
}
