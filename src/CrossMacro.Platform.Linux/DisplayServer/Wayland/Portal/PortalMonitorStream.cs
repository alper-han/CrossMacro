namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Portal;

internal readonly record struct PortalMonitorStream(PortalStreamDescriptor Stream, string? Id, ScreenRect Bounds);
