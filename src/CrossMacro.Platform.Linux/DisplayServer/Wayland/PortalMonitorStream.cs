namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal readonly record struct PortalMonitorStream(PortalStream Stream, string? Id, ScreenRect Bounds);
