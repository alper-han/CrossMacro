using System.Text.Json.Serialization;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriFocusedWindowData
{
    [JsonPropertyName("FocusedWindow")]
    public NiriWindowDto? FocusedWindow { get; set; }
}
