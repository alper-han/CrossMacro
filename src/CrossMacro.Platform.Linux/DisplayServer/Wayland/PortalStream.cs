using CrossMacro.Platform.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public readonly record struct PortalStream(uint NodeId, IReadOnlyDictionary<string, object> Properties);
