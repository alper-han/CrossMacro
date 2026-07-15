using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

[JsonSerializable(typeof(WindowInfo))]
[JsonSerializable(typeof(WindowInfo[]))]
internal sealed partial class KdeJsonContext : JsonSerializerContext
{
}
