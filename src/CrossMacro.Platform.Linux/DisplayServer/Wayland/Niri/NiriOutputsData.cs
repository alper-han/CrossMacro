
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Niri;

public sealed class NiriOutputsData
{
    [JsonPropertyName("Outputs")]
    public IDictionary<string, NiriOutputDto>? Outputs { get; init; }
}
