
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// The time budget for a single compositor round trip, setup step or capability probe.
/// </summary>
internal static class WaylandTimeouts
{
    public static readonly TimeSpan Operation = TimeSpan.FromSeconds(2);
}
