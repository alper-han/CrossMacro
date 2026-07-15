
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

[JsonSerializable(typeof(HyprlandWindowDto))]
[JsonSerializable(typeof(HyprlandWindowDto[]))]
[JsonSerializable(typeof(HyprlandActiveWorkspaceDto))]
/// <summary>
/// Window manager implementation using Hyprland IPC socket commands.
/// </summary>
internal sealed partial class HyprlandJsonContext : JsonSerializerContext;
