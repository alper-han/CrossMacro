
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Niri;

[JsonSerializable(typeof(NiriResponse<NiriFocusedWindowData>))]
[JsonSerializable(typeof(NiriResponse<NiriWindowsData>))]
[JsonSerializable(typeof(NiriResponse<NiriWorkspacesData>))]
[JsonSerializable(typeof(NiriResponse<NiriOutputsData>))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class NiriJsonContext : JsonSerializerContext;
