namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public readonly record struct PortalStreamDescriptor(uint NodeId, IReadOnlyDictionary<string, object> Properties);
