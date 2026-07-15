using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

[JsonSerializable(typeof(HyprlandWindowDto))]
[JsonSerializable(typeof(HyprlandWindowDto[]))]
[JsonSerializable(typeof(HyprlandActiveWorkspaceDto))]
/// <summary>
/// Window manager implementation using Hyprland IPC socket commands.
/// </summary>
internal sealed partial class HyprlandJsonContext : JsonSerializerContext
{
}
