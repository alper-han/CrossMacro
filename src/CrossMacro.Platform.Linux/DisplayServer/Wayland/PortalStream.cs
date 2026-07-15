
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public readonly record struct PortalStream(uint NodeId, IReadOnlyDictionary<string, object> Properties);
