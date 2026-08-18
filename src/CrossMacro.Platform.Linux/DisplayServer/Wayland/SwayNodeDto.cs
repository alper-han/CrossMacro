
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class SwayNodeDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("focused")]
    public bool Focused { get; set; }

    [JsonPropertyName("pid")]
    public int? Pid { get; set; }

    [JsonPropertyName("app_id")]
    public string? AppId { get; set; }

    [JsonPropertyName("fullscreen_mode")]
    public int? FullscreenMode { get; set; }

    [JsonPropertyName("sticky")]
    public bool Sticky { get; set; }

    [JsonPropertyName("window_properties")]
    public SwayWindowPropertiesDto? WindowProperties { get; set; }

    [JsonPropertyName("rect")]
    public SwayRectDto? Rect { get; set; }

    [JsonPropertyName("nodes")]
    public SwayNodeDto[]? Nodes { get; set; }

    [JsonPropertyName("floating_nodes")]
    public SwayNodeDto[]? FloatingNodes { get; set; }
}
