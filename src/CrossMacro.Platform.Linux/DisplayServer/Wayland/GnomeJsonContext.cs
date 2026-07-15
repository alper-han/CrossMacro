using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

[System.Text.Json.Serialization.JsonSerializable(typeof(WindowInfo))]
[System.Text.Json.Serialization.JsonSerializable(typeof(WindowInfo[]))]
internal sealed partial class GnomeJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
