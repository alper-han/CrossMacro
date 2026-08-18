
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriOutputDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("logical")]
    public NiriLogicalGeometryDto? Logical { get; set; }
}
