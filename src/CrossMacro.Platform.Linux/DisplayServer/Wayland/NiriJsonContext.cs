using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

[JsonSerializable(typeof(NiriResponse<NiriFocusedWindowData>))]
[JsonSerializable(typeof(NiriResponse<NiriWindowsData>))]
[JsonSerializable(typeof(NiriResponse<NiriWorkspacesData>))]
[JsonSerializable(typeof(NiriResponse<NiriOutputsData>))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class NiriJsonContext : JsonSerializerContext
{
}
