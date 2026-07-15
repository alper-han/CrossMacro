namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal readonly record struct PortalMonitorStream(PortalStreamDescriptor Stream, string? Id, ScreenRect Bounds);
