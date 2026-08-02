namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandClipboardProtocol : IDisposable
{
    public WaylandClipboardProtocol()
    {
        WlRegistry = new("wl_registry", 1, [("bind", "usun")], [("global", "usu"), ("global_remove", "u")]);
        WlSeat = new("wl_seat", 7, [("get_pointer", "n"), ("get_keyboard", "n"), ("get_touch", "n"), ("release", "")], [("capabilities", "u"), ("name", "s")]);
        WlKeyboard = new("wl_keyboard", 7, [("release", "")], [("keymap", "uhu"), ("enter", "uoa"), ("leave", "uo"), ("key", "uuuu"), ("modifiers", "uuuuu"), ("repeat_info", "ii")]);
        WlCompositor = new("wl_compositor", 4, [("create_surface", "n"), ("create_region", "n"), ("release", "")], []);
        WlRegion = new("wl_region", 1, [("destroy", ""), ("add", "iiii"), ("subtract", "iiii")], []);
        WlShm = new("wl_shm", 1, [("create_pool", "nhi")], [("format", "u")]);
        WlShmPool = new("wl_shm_pool", 1, [("create_buffer", "niiiiu"), ("destroy", ""), ("resize", "i")], []);
        WlBuffer = new("wl_buffer", 1, [("destroy", "")], [("release", "")]);
        WlCallback = new("wl_callback", 1, [], [("done", "u")]);
        WlSurface = new("wl_surface", 4, [("destroy", ""), ("attach", "oii"), ("damage", "iiii"), ("frame", "n"), ("set_opaque_region", "o"), ("set_input_region", "o"), ("commit", ""), ("set_buffer_transform", "i"), ("set_buffer_scale", "i"), ("damage_buffer", "iiii"), ("offset", "ii")], [("enter", "o"), ("leave", "o")]);
        WlOutput = new("wl_output", 4, [], []);
        WlShell = new("wl_shell", 1, [("get_shell_surface", "no")], []);
        WlShellSurface = new("wl_shell_surface", 1, [("pong", "u"), ("move", "ou"), ("resize", "ouu"), ("set_toplevel", ""), ("set_transient", "oiiu"), ("set_fullscreen", "uuoo"), ("set_popup", "ouoiiu"), ("set_maximized", "o"), ("set_title", "s"), ("set_class", "s")], [("ping", "u"), ("configure", "uii"), ("popup_done", "")]);
        WlDataDeviceManager = new("wl_data_device_manager", 3, [("create_data_source", "n"), ("get_data_device", "no")], []);
        WlDataDevice = new(
            "wl_data_device",
            3,
            [("start_drag", "ooou"), ("set_selection", "ou"), ("release", "")],
            [("data_offer", "n"), ("enter", "uoff"), ("leave", ""), ("motion", "uff"), ("drop", ""), ("selection", "o"), ("dnd_action", "u"), ("action", "u")]);
        WlDataSource = new(
            "wl_data_source",
            3,
            [("offer", "s"), ("destroy", "")],
            [("target", "s"), ("send", "sh"), ("cancelled", ""), ("dnd_drop_performed", ""), ("dnd_finished", ""), ("action", "u")]);
        WlDataOffer = new(
            "wl_data_offer",
            3,
            [("accept", "us"), ("receive", "sh"), ("destroy", ""), ("finish", ""), ("set_actions", "uu")],
            [("offer", "s"), ("source_actions", "u"), ("action", "u")]);

        ExtDataControlManager = new(
            "ext_data_control_manager_v1",
            1,
            [("create_data_source", "n"), ("get_data_device", "no"), ("destroy", "")],
            []);
        ExtDataControlDevice = new(
            "ext_data_control_device_v1",
            1,
            [("set_selection", "o"), ("destroy", ""), ("set_primary_selection", "o")],
            [("data_offer", "n"), ("selection", "o"), ("finished", ""), ("primary_selection", "o")]);
        ExtDataControlSource = new(
            "ext_data_control_source_v1",
            1,
            [("offer", "s"), ("destroy", "")],
            [("send", "sh"), ("cancelled", "")]);
        ExtDataControlOffer = new(
            "ext_data_control_offer_v1",
            1,
            [("receive", "sh"), ("destroy", "")],
            [("offer", "s")]);

        WlrDataControlManager = new(
            "zwlr_data_control_manager_v1",
            2,
            [("create_data_source", "n"), ("get_data_device", "no"), ("destroy", "")],
            []);
        WlrDataControlDevice = new(
            "zwlr_data_control_device_v1",
            2,
            [("set_selection", "o"), ("destroy", ""), ("set_primary_selection", "o")],
            [("data_offer", "n"), ("selection", "o"), ("finished", ""), ("primary_selection", "o")]);
        WlrDataControlSource = new(
            "zwlr_data_control_source_v1",
            1,
            [("offer", "s"), ("destroy", "")],
            [("send", "sh"), ("cancelled", "")]);
        WlrDataControlOffer = new(
            "zwlr_data_control_offer_v1",
            1,
            [("receive", "sh"), ("destroy", "")],
            [("offer", "s")]);

        XdgWmBase = new("xdg_wm_base", 1, [("destroy", ""), ("create_positioner", "n"), ("get_xdg_surface", "no"), ("pong", "u")], [("ping", "u")]);
        XdgSurface = new("xdg_surface", 1, [("destroy", ""), ("get_toplevel", "n"), ("get_popup", "noo"), ("set_window_geometry", "iiii"), ("ack_configure", "u")], [("configure", "u")]);
        XdgToplevel = new("xdg_toplevel", 1, [("destroy", ""), ("set_parent", "o"), ("set_title", "s"), ("set_app_id", "s"), ("show_window_menu", "ouii"), ("move", "ou"), ("resize", "ouu"), ("set_max_size", "ii"), ("set_min_size", "ii"), ("set_maximized", ""), ("unset_maximized", ""), ("set_fullscreen", "o"), ("unset_fullscreen", ""), ("set_minimized", "")], [("configure", "iia"), ("close", "")]);

        WlDataDeviceManager.SetMethodTypes(0, WlDataSource.Address);
        WlDataDeviceManager.SetMethodTypes(1, WlDataDevice.Address, WlSeat.Address);
        WlSeat.SetMethodTypes(1, WlKeyboard.Address);
        WlCompositor.SetMethodTypes(0, WlSurface.Address);
        WlShm.SetMethodTypes(0, WlShmPool.Address, IntPtr.Zero, IntPtr.Zero);
        WlShmPool.SetMethodTypes(0, WlBuffer.Address);
        WlSurface.SetMethodTypes(1, WlBuffer.Address, IntPtr.Zero, IntPtr.Zero);
        WlSurface.SetMethodTypes(3, WlCallback.Address);
        WlShell.SetMethodTypes(0, WlShellSurface.Address, WlSurface.Address);
        WlDataDevice.SetMethodTypes(0, WlDataSource.Address, WlSurface.Address, IntPtr.Zero, IntPtr.Zero);
        WlDataDevice.SetMethodTypes(1, WlDataSource.Address);
        WlDataDevice.SetMethodTypes(2);
        WlDataSource.SetMethodTypes(1);
        WlDataOffer.SetMethodTypes(1);

        WlCompositor.SetMethodTypes(1, WlRegion.Address);
        WlSurface.SetMethodTypes(4, WlRegion.Address);
        WlSurface.SetMethodTypes(5, WlRegion.Address);

        WlKeyboard.SetEventTypes(1, IntPtr.Zero, WlSurface.Address, IntPtr.Zero);
        WlKeyboard.SetEventTypes(2, IntPtr.Zero, WlSurface.Address);
        WlSurface.SetEventTypes(0, WlOutput.Address);
        WlSurface.SetEventTypes(1, WlOutput.Address);
        WlDataDevice.SetEventTypes(0, WlDataOffer.Address);
        WlDataDevice.SetEventTypes(1, IntPtr.Zero, WlSurface.Address, IntPtr.Zero, IntPtr.Zero);
        WlDataDevice.SetEventTypes(5, WlDataOffer.Address);

        ExtDataControlManager.SetMethodTypes(0, ExtDataControlSource.Address);
        ExtDataControlManager.SetMethodTypes(1, ExtDataControlDevice.Address, WlSeat.Address);
        ExtDataControlDevice.SetMethodTypes(0, ExtDataControlSource.Address);
        ExtDataControlDevice.SetMethodTypes(2, ExtDataControlSource.Address);
        ExtDataControlSource.SetMethodTypes(1);
        ExtDataControlOffer.SetMethodTypes(0);
        ExtDataControlDevice.SetEventTypes(0, ExtDataControlOffer.Address);
        ExtDataControlDevice.SetEventTypes(1, ExtDataControlOffer.Address);
        ExtDataControlDevice.SetEventTypes(3, ExtDataControlOffer.Address);

        WlrDataControlManager.SetMethodTypes(0, WlrDataControlSource.Address);
        WlrDataControlManager.SetMethodTypes(1, WlrDataControlDevice.Address, WlSeat.Address);
        WlrDataControlDevice.SetMethodTypes(0, WlrDataControlSource.Address);
        WlrDataControlDevice.SetMethodTypes(2, WlrDataControlSource.Address);
        WlrDataControlSource.SetMethodTypes(1);
        WlrDataControlOffer.SetMethodTypes(0);
        WlrDataControlDevice.SetEventTypes(0, WlrDataControlOffer.Address);
        WlrDataControlDevice.SetEventTypes(1, WlrDataControlOffer.Address);
        WlrDataControlDevice.SetEventTypes(3, WlrDataControlOffer.Address);

        XdgWmBase.SetMethodTypes(2, XdgSurface.Address, WlSurface.Address);
        XdgSurface.SetMethodTypes(1, XdgToplevel.Address);
    }

    public WaylandInterfaceHandle WlRegistry { get; }
    public WaylandInterfaceHandle WlSeat { get; }
    public WaylandInterfaceHandle WlKeyboard { get; }
    public WaylandInterfaceHandle WlCompositor { get; }
    public WaylandInterfaceHandle WlRegion { get; }
    public WaylandInterfaceHandle WlShm { get; }
    public WaylandInterfaceHandle WlShmPool { get; }
    public WaylandInterfaceHandle WlBuffer { get; }
    public WaylandInterfaceHandle WlCallback { get; }
    public WaylandInterfaceHandle WlSurface { get; }
    public WaylandInterfaceHandle WlOutput { get; }
    public WaylandInterfaceHandle WlShell { get; }
    public WaylandInterfaceHandle WlShellSurface { get; }
    public WaylandInterfaceHandle WlDataDeviceManager { get; }
    public WaylandInterfaceHandle WlDataDevice { get; }
    public WaylandInterfaceHandle WlDataSource { get; }
    public WaylandInterfaceHandle WlDataOffer { get; }
    public WaylandInterfaceHandle ExtDataControlManager { get; }
    public WaylandInterfaceHandle ExtDataControlDevice { get; }
    public WaylandInterfaceHandle ExtDataControlSource { get; }
    public WaylandInterfaceHandle ExtDataControlOffer { get; }
    public WaylandInterfaceHandle WlrDataControlManager { get; }
    public WaylandInterfaceHandle WlrDataControlDevice { get; }
    public WaylandInterfaceHandle WlrDataControlSource { get; }
    public WaylandInterfaceHandle WlrDataControlOffer { get; }
    public WaylandInterfaceHandle XdgWmBase { get; }
    public WaylandInterfaceHandle XdgSurface { get; }
    public WaylandInterfaceHandle XdgToplevel { get; }

    public void Dispose()
    {
        WlRegistry.Dispose();
        WlSeat.Dispose();
        WlKeyboard.Dispose();
        WlCompositor.Dispose();
        WlRegion.Dispose();
        WlShm.Dispose();
        WlShmPool.Dispose();
        WlBuffer.Dispose();
        WlCallback.Dispose();
        WlSurface.Dispose();
        WlOutput.Dispose();
        WlShell.Dispose();
        WlShellSurface.Dispose();
        WlDataDeviceManager.Dispose();
        WlDataDevice.Dispose();
        WlDataSource.Dispose();
        WlDataOffer.Dispose();
        ExtDataControlManager.Dispose();
        ExtDataControlDevice.Dispose();
        ExtDataControlSource.Dispose();
        ExtDataControlOffer.Dispose();
        WlrDataControlManager.Dispose();
        WlrDataControlDevice.Dispose();
        WlrDataControlSource.Dispose();
        WlrDataControlOffer.Dispose();
        XdgWmBase.Dispose();
        XdgSurface.Dispose();
        XdgToplevel.Dispose();
    }
}
