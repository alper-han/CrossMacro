namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct OutputLayout(int OriginX, int OriginY, int Width, int Height);
