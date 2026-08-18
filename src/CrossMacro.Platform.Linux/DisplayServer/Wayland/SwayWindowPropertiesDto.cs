
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class SwayWindowPropertiesDto
{
    [JsonPropertyName("class")]
    public string? Class { get; set; }

    [JsonPropertyName("instance")]
    public string? Instance { get; set; }
}
