namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandProtocolTables : IDisposable
{
    public WaylandProtocolTables()
    {
        WlRegistry = new("wl_registry", 1, [("bind", "usun")], [("global", "usu"), ("global_remove", "u")]);
        WlOutput = new("wl_output", 4, [], [("geometry", "iiiiissi"), ("mode", "uiii"), ("done", ""), ("scale", "i"), ("name", "s"), ("description", "s")]);
        WlShm = new("wl_shm", 1, [("create_pool", "nhi")], [("format", "u")]);
        WlShmPool = new("wl_shm_pool", 1, [("create_buffer", "niiiiu"), ("destroy", ""), ("resize", "i")], []);
        WlBuffer = new("wl_buffer", 1, [("destroy", "")], [("release", "")]);
        XdgOutputManager = new("zxdg_output_manager_v1", 3, [("destroy", ""), ("get_xdg_output", "no")], []);
        XdgOutput = new("zxdg_output_v1", 3, [("destroy", "")], [("logical_position", "ii"), ("logical_size", "ii"), ("done", ""), ("name", "s"), ("description", "s")]);
        ExtOutputSourceManager = new("ext_output_image_capture_source_manager_v1", 1, [("create_source", "no"), ("destroy", "")], []);
        ExtCaptureSource = new("ext_image_capture_source_v1", 1, [("destroy", "")], []);
        ExtCopyManager = new("ext_image_copy_capture_manager_v1", 1, [("create_session", "nou"), ("create_pointer_cursor_session", "noo"), ("destroy", "")], []);
        ExtCopySession = new("ext_image_copy_capture_session_v1", 1, [("create_frame", "n"), ("destroy", "")], [("buffer_size", "uu"), ("shm_format", "u"), ("dmabuf_device", "a"), ("dmabuf_format", "ua"), ("done", ""), ("stopped", "")]);
        ExtCopyFrame = new("ext_image_copy_capture_frame_v1", 1, [("destroy", ""), ("attach_buffer", "o"), ("damage_buffer", "iiii"), ("capture", "")], [("transform", "u"), ("damage", "iiii"), ("presentation_time", "uuu"), ("ready", ""), ("failed", "u")]);
        WlrScreencopyManager = new("zwlr_screencopy_manager_v1", 3, [("capture_output", "nuo"), ("capture_output_region", "nuoiiii"), ("destroy", "")], []);
        WlrScreencopyFrame = new("zwlr_screencopy_frame_v1", 3, [("copy", "o"), ("destroy", ""), ("copy_with_damage", "o")], [("buffer", "uuuu"), ("flags", "u"), ("ready", "uuu"), ("failed", ""), ("damage", "uuuu"), ("linux_dmabuf", "uuu"), ("buffer_done", "")]);

        WlShm.SetMethodTypes(0, WlShmPool.Address, IntPtr.Zero, IntPtr.Zero);
        WlShmPool.SetMethodTypes(0, WlBuffer.Address, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        XdgOutputManager.SetMethodTypes(1, XdgOutput.Address, WlOutput.Address);
        ExtOutputSourceManager.SetMethodTypes(0, ExtCaptureSource.Address, WlOutput.Address);
        ExtCopyManager.SetMethodTypes(0, ExtCopySession.Address, ExtCaptureSource.Address, IntPtr.Zero);
        ExtCopySession.SetMethodTypes(0, ExtCopyFrame.Address);
        ExtCopyFrame.SetMethodTypes(1, WlBuffer.Address);
        WlrScreencopyManager.SetMethodTypes(0, WlrScreencopyFrame.Address, IntPtr.Zero, WlOutput.Address);
        WlrScreencopyManager.SetMethodTypes(1, WlrScreencopyFrame.Address, IntPtr.Zero, WlOutput.Address, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        WlrScreencopyFrame.SetMethodTypes(0, WlBuffer.Address);
    }

    public WaylandInterfaceHandle WlRegistry { get; }
    public WaylandInterfaceHandle WlOutput { get; }
    public WaylandInterfaceHandle WlShm { get; }
    public WaylandInterfaceHandle WlShmPool { get; }
    public WaylandInterfaceHandle WlBuffer { get; }
    public WaylandInterfaceHandle XdgOutputManager { get; }
    public WaylandInterfaceHandle XdgOutput { get; }
    public WaylandInterfaceHandle ExtOutputSourceManager { get; }
    public WaylandInterfaceHandle ExtCaptureSource { get; }
    public WaylandInterfaceHandle ExtCopyManager { get; }
    public WaylandInterfaceHandle ExtCopySession { get; }
    public WaylandInterfaceHandle ExtCopyFrame { get; }
    public WaylandInterfaceHandle WlrScreencopyManager { get; }
    public WaylandInterfaceHandle WlrScreencopyFrame { get; }

    public void Dispose()
    {
        WlRegistry.Dispose();
        WlOutput.Dispose();
        WlShm.Dispose();
        WlShmPool.Dispose();
        WlBuffer.Dispose();
        XdgOutputManager.Dispose();
        XdgOutput.Dispose();
        ExtOutputSourceManager.Dispose();
        ExtCaptureSource.Dispose();
        ExtCopyManager.Dispose();
        ExtCopySession.Dispose();
        ExtCopyFrame.Dispose();
        WlrScreencopyManager.Dispose();
        WlrScreencopyFrame.Dispose();
    }
}
