using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriOutputsData
{
    [JsonPropertyName("Outputs")]
    public Dictionary<string, NiriOutputDto>? Outputs { get; set; }
}
