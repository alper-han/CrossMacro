
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class SwayWorkspaceDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("focused")]
    public bool Focused { get; set; }
}
