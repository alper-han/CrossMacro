namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Protocols.ExtImageCopy;


internal static class WaylandExtImageCopyRegistryProbe
{
    public static WaylandSupportResult Probe(LinuxEnvironmentSnapshot environment)
    {
        var waylandDisplay = environment.WaylandDisplay;
        if (string.IsNullOrWhiteSpace(waylandDisplay))
        {
            return WaylandSupportResult.Unsupported("WAYLAND_DISPLAY is not set; ext-image-copy-capture-v1 requires a Wayland session.");
        }

        using var connection = WaylandWlrConnection.Connect(new ScreenReadOptions(timeout: WaylandTimeouts.Operation));
        if (connection.Registry.Shm == IntPtr.Zero)
        {
            return WaylandSupportResult.Unsupported("Wayland registry did not expose wl_shm.");
        }

        if (connection.Registry.ExtOutputSourceManager == IntPtr.Zero)
        {
            return WaylandSupportResult.Unsupported("Wayland registry did not expose ext_output_image_capture_source_manager_v1.");
        }

        if (connection.Registry.ExtCopyManager == IntPtr.Zero)
        {
            return WaylandSupportResult.Unsupported("Wayland registry did not expose ext_image_copy_capture_manager_v1.");
        }

        if (connection.Registry.Outputs.Count is 0)
        {
            return WaylandSupportResult.Unsupported("Wayland registry did not expose any wl_output globals.");
        }

        return WaylandSupportResult.Supported();
    }

    /// <summary>
    /// Captures the live environment at call time. Prefer the snapshot-backed
    /// probe in production composition so the environment is captured once at
    /// the boundary and passed through.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    internal static WaylandSupportResult Probe()
    {
        return Probe(LinuxEnvironmentVariables.CaptureCurrentSnapshot());
    }
}
