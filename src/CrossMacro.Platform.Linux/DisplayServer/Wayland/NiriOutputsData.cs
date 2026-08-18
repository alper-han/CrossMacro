
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriOutputsData
{
    [JsonPropertyName("Outputs")]
    public IDictionary<string, NiriOutputDto>? Outputs { get; init; }
}
