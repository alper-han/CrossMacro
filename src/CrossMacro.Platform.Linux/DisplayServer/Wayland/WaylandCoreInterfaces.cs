
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// The core <c>wl_*</c> interface descriptors every protocol table in this folder needs. Each call creates a handle
/// that the caller owns; per-use argument types are attached by the caller with <c>SetMethodTypes</c>/<c>SetEventTypes</c>.
/// </summary>
internal static class WaylandCoreInterfaces
{
    public static WaylandInterfaceHandle CreateRegistry() =>
        new("wl_registry", 1, [("bind", "usun")], [("global", "usu"), ("global_remove", "u")]);

    public static WaylandInterfaceHandle CreateCallback() =>
        new("wl_callback", 1, [], [("done", "u")]);

    public static WaylandInterfaceHandle CreateSeat() =>
        new("wl_seat", 7, [("get_pointer", "n"), ("get_keyboard", "n"), ("get_touch", "n"), ("release", "")], [("capabilities", "u"), ("name", "s")]);

    public static WaylandInterfaceHandle CreateOutput() =>
        new("wl_output", 4, [], [("geometry", "iiiiissi"), ("mode", "uiii"), ("done", ""), ("scale", "i"), ("name", "s"), ("description", "s")]);

    public static WaylandInterfaceHandle CreateShm() =>
        new("wl_shm", 1, [("create_pool", "nhi")], [("format", "u")]);

    public static WaylandInterfaceHandle CreateShmPool() =>
        new("wl_shm_pool", 1, [("create_buffer", "niiiiu"), ("destroy", ""), ("resize", "i")], []);

    public static WaylandInterfaceHandle CreateBuffer() =>
        new("wl_buffer", 1, [("destroy", "")], [("release", "")]);
}
