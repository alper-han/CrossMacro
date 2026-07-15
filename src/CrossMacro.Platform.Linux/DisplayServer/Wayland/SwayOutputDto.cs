
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class SwayOutputDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("rect")]
    public SwayRectDto? Rect { get; set; }
}
