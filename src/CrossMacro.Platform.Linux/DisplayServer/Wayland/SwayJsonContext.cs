using System.Text.Json.Serialization;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

// DTOs for Sway JSON responses

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

internal sealed class SwayWindowPropertiesDto
{
    [JsonPropertyName("class")]
    public string? Class { get; set; }

    [JsonPropertyName("instance")]
    public string? Instance { get; set; }
}

internal sealed class SwayWorkspaceDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("focused")]
    public bool Focused { get; set; }
}

internal sealed class SwayOutputDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("rect")]
    public SwayRectDto? Rect { get; set; }
}

internal sealed class SwayRectDto
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

internal sealed class SwayCommandResultDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

[JsonSerializable(typeof(SwayNodeDto))]
[JsonSerializable(typeof(SwayWorkspaceDto[]))]
[JsonSerializable(typeof(SwayOutputDto[]))]
[JsonSerializable(typeof(SwayCommandResultDto[]))]
internal sealed partial class SwayJsonContext : JsonSerializerContext
{
}
