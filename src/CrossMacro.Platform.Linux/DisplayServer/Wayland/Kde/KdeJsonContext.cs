
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Kde;

[JsonSerializable(typeof(WindowInfo))]
[JsonSerializable(typeof(WindowInfo[]))]
internal sealed partial class KdeJsonContext : JsonSerializerContext;
