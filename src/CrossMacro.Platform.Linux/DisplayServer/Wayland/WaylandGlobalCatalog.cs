
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Which advertised registry globals the client binds, at which version, and which
/// global name currently fills each singleton slot.
/// </summary>
internal sealed class WaylandGlobalCatalog
{
    /// <summary>
    /// <c>buffer_done</c> exists since version 3; the capture flow only creates a buffer after it,
    /// so older managers could never complete a capture.
    /// </summary>
    internal const uint MinimumWlrScreencopyVersion = 3;

    private readonly Dictionary<uint, WaylandGlobalSlot> _singletonOwners = [];

    public static WaylandGlobalMatch Resolve(string iface, uint advertisedVersion, out WaylandGlobalSlot slot, out uint bindVersion)
    {
        ArgumentNullException.ThrowIfNull(iface);
        uint highestVersion;
        uint lowestVersion = 1;
        switch (iface)
        {
            case "wl_output":
                (slot, highestVersion) = (WaylandGlobalSlot.Output, 4);
                break;
            case "wl_seat":
                (slot, highestVersion) = (WaylandGlobalSlot.Seat, 1);
                break;
            case "wl_shm":
                (slot, highestVersion) = (WaylandGlobalSlot.Shm, 1);
                break;
            case "zwlr_screencopy_manager_v1":
                (slot, highestVersion) = (WaylandGlobalSlot.WlrScreencopyManager, 3);
                lowestVersion = MinimumWlrScreencopyVersion;
                break;
            case "zxdg_output_manager_v1":
                (slot, highestVersion) = (WaylandGlobalSlot.XdgOutputManager, 3);
                break;
            case "ext_output_image_capture_source_manager_v1":
                (slot, highestVersion) = (WaylandGlobalSlot.ExtOutputSourceManager, 1);
                break;
            case "ext_image_copy_capture_manager_v1":
                (slot, highestVersion) = (WaylandGlobalSlot.ExtCopyManager, 1);
                break;
            default:
                slot = default;
                bindVersion = 0;
                return WaylandGlobalMatch.Unknown;
        }

        if (advertisedVersion < lowestVersion)
        {
            bindVersion = 0;
            return WaylandGlobalMatch.VersionTooOld;
        }

        bindVersion = Math.Min(advertisedVersion, highestVersion);
        return WaylandGlobalMatch.Bindable;
    }

    /// <summary>Remembers that the global <paramref name="name"/> filled <paramref name="slot"/>.</summary>
    public void RecordSingleton(uint name, WaylandGlobalSlot slot) => _singletonOwners[name] = slot;

    /// <summary>The singleton slot the withdrawn global <paramref name="name"/> filled, if any.</summary>
    public bool TryReleaseSingleton(uint name, out WaylandGlobalSlot slot) => _singletonOwners.Remove(name, out slot);
}
