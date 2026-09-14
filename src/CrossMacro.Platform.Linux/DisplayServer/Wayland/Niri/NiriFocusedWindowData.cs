
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Niri;

public sealed class NiriFocusedWindowData
{
    [JsonPropertyName("FocusedWindow")]
    public NiriWindowDto? FocusedWindow { get; set; }
}
