namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal static class WaylandExtImageCopyRegistryProbe
{
    public static ExtImageCopySupportResult Probe()
    {
        var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        if (string.IsNullOrWhiteSpace(waylandDisplay))
        {
            return ExtImageCopySupportResult.Unsupported("WAYLAND_DISPLAY is not set; ext-image-copy-capture-v1 requires a Wayland session.");
        }

        using var connection = WaylandWlrConnection.Connect();
        if (connection.Registry.Shm == IntPtr.Zero)
        {
            return ExtImageCopySupportResult.Unsupported("Wayland registry did not expose wl_shm.");
        }

        if (connection.Registry.ExtOutputSourceManager == IntPtr.Zero)
        {
            return ExtImageCopySupportResult.Unsupported("Wayland registry did not expose ext_output_image_capture_source_manager_v1.");
        }

        if (connection.Registry.ExtCopyManager == IntPtr.Zero)
        {
            return ExtImageCopySupportResult.Unsupported("Wayland registry did not expose ext_image_copy_capture_manager_v1.");
        }

        if (connection.Registry.Outputs.Count == 0)
        {
            return ExtImageCopySupportResult.Unsupported("Wayland registry did not expose any wl_output globals.");
        }

        return ExtImageCopySupportResult.Supported();
    }
}
