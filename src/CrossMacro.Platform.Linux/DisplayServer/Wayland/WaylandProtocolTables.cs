namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandProtocolTables : IDisposable
{
    public WaylandProtocolTables()
    {
        WlRegistry = WaylandCoreInterfaces.CreateRegistry();
        WlSeat = WaylandCoreInterfaces.CreateSeat();
        WlPointer = new("wl_pointer", 1, [("set_cursor", "uoii")], [("enter", "uoff"), ("leave", "uo"), ("motion", "uff"), ("button", "uuuu"), ("axis", "uuf")]);
        WlOutput = WaylandCoreInterfaces.CreateOutput();
        WlShm = WaylandCoreInterfaces.CreateShm();
        WlShmPool = WaylandCoreInterfaces.CreateShmPool();
        WlBuffer = WaylandCoreInterfaces.CreateBuffer();
        XdgOutputManager = new("zxdg_output_manager_v1", 3, [("destroy", ""), ("get_xdg_output", "no")], []);
        XdgOutput = new("zxdg_output_v1", 3, [("destroy", "")], [("logical_position", "ii"), ("logical_size", "ii"), ("done", ""), ("name", "s"), ("description", "s")]);
        ExtOutputSourceManager = new("ext_output_image_capture_source_manager_v1", 1, [("create_source", "no"), ("destroy", "")], []);
        ExtCaptureSource = new("ext_image_capture_source_v1", 1, [("destroy", "")], []);
        ExtCopyManager = new("ext_image_copy_capture_manager_v1", 1, [("create_session", "nou"), ("create_pointer_cursor_session", "noo"), ("destroy", "")], []);
        ExtCursorSession = new("ext_image_copy_capture_cursor_session_v1", 1, [("destroy", ""), ("get_capture_session", "n")], [("enter", ""), ("leave", ""), ("position", "ii"), ("hotspot", "ii")]);
        ExtCopySession = new("ext_image_copy_capture_session_v1", 1, [("create_frame", "n"), ("destroy", "")], [("buffer_size", "uu"), ("shm_format", "u"), ("dmabuf_device", "a"), ("dmabuf_format", "ua"), ("done", ""), ("stopped", "")]);
        ExtCopyFrame = new("ext_image_copy_capture_frame_v1", 1, [("destroy", ""), ("attach_buffer", "o"), ("damage_buffer", "iiii"), ("capture", "")], [("transform", "u"), ("damage", "iiii"), ("presentation_time", "uuu"), ("ready", ""), ("failed", "u")]);
        WlrScreencopyManager = new("zwlr_screencopy_manager_v1", 3, [("capture_output", "nuo"), ("capture_output_region", "nuoiiii"), ("destroy", "")], []);
        WlrScreencopyFrame = new("zwlr_screencopy_frame_v1", 3, [("copy", "o"), ("destroy", ""), ("copy_with_damage", "o")], [("buffer", "uuuu"), ("flags", "u"), ("ready", "uuu"), ("failed", ""), ("damage", "uuuu"), ("linux_dmabuf", "uuu"), ("buffer_done", "")]);

        WlSeat.SetMethodTypes(0, WlPointer.Address);
        WlShm.SetMethodTypes(0, WlShmPool.Address, IntPtr.Zero, IntPtr.Zero);
        WlShmPool.SetMethodTypes(0, WlBuffer.Address, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        XdgOutputManager.SetMethodTypes(1, XdgOutput.Address, WlOutput.Address);
        ExtOutputSourceManager.SetMethodTypes(0, ExtCaptureSource.Address, WlOutput.Address);
        ExtCopyManager.SetMethodTypes(0, ExtCopySession.Address, ExtCaptureSource.Address, IntPtr.Zero);
        ExtCopyManager.SetMethodTypes(1, ExtCursorSession.Address, ExtCaptureSource.Address, WlPointer.Address);
        ExtCursorSession.SetMethodTypes(1, ExtCopySession.Address);
        ExtCopySession.SetMethodTypes(0, ExtCopyFrame.Address);
        ExtCopyFrame.SetMethodTypes(1, WlBuffer.Address);
        WlrScreencopyManager.SetMethodTypes(0, WlrScreencopyFrame.Address, IntPtr.Zero, WlOutput.Address);
        WlrScreencopyManager.SetMethodTypes(1, WlrScreencopyFrame.Address, IntPtr.Zero, WlOutput.Address, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        WlrScreencopyFrame.SetMethodTypes(0, WlBuffer.Address);
    }

    public WaylandInterfaceHandle WlRegistry { get; }
    public WaylandInterfaceHandle WlSeat { get; }
    public WaylandInterfaceHandle WlPointer { get; }
    public WaylandInterfaceHandle WlOutput { get; }
    public WaylandInterfaceHandle WlShm { get; }
    public WaylandInterfaceHandle WlShmPool { get; }
    public WaylandInterfaceHandle WlBuffer { get; }
    public WaylandInterfaceHandle XdgOutputManager { get; }
    public WaylandInterfaceHandle XdgOutput { get; }
    public WaylandInterfaceHandle ExtOutputSourceManager { get; }
    public WaylandInterfaceHandle ExtCaptureSource { get; }
    public WaylandInterfaceHandle ExtCopyManager { get; }
    public WaylandInterfaceHandle ExtCursorSession { get; }
    public WaylandInterfaceHandle ExtCopySession { get; }
    public WaylandInterfaceHandle ExtCopyFrame { get; }
    public WaylandInterfaceHandle WlrScreencopyManager { get; }
    public WaylandInterfaceHandle WlrScreencopyFrame { get; }

    public void Dispose()
    {
        WlRegistry.Dispose();
        WlSeat.Dispose();
        WlPointer.Dispose();
        WlOutput.Dispose();
        WlShm.Dispose();
        WlShmPool.Dispose();
        WlBuffer.Dispose();
        XdgOutputManager.Dispose();
        XdgOutput.Dispose();
        ExtOutputSourceManager.Dispose();
        ExtCaptureSource.Dispose();
        ExtCopyManager.Dispose();
        ExtCursorSession.Dispose();
        ExtCopySession.Dispose();
        ExtCopyFrame.Dispose();
        WlrScreencopyManager.Dispose();
        WlrScreencopyFrame.Dispose();
    }
}
