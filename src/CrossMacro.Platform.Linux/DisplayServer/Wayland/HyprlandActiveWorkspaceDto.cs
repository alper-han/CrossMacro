using System.Text.Json.Serialization;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class HyprlandActiveWorkspaceDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
