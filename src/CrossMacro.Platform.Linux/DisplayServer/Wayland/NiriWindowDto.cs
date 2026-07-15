using System.Text.Json.Serialization;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriWindowDto
{
    [JsonPropertyName("id")]
    public ulong Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("app_id")]
    public string? AppId { get; set; }

    [JsonPropertyName("pid")]
    public int? Pid { get; set; }

    [JsonPropertyName("workspace_id")]
    public ulong? WorkspaceId { get; set; }

    [JsonPropertyName("is_focused")]
    public bool IsFocused { get; set; }

    [JsonPropertyName("is_floating")]
    public bool IsFloating { get; set; }

    [JsonPropertyName("is_urgent")]
    public bool IsUrgent { get; set; }

    [JsonPropertyName("layout")]
    public NiriLayoutDto? Layout { get; set; }
}
