using System.Text.Json.Serialization;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriWorkspaceDto
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("idx")]
    public int Idx { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("output")]
    public string? Output { get; set; }

    [JsonPropertyName("is_urgent")]
    public bool IsUrgent { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("is_focused")]
    public bool IsFocused { get; set; }

    [JsonPropertyName("active_window_id")]
    public ulong? ActiveWindowId { get; set; }
}
