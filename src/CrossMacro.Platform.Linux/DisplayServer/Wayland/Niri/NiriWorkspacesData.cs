
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Niri;

public sealed class NiriWorkspacesData
{
    [JsonPropertyName("Workspaces")]
    public IReadOnlyList<NiriWorkspaceDto>? Workspaces { get; set; }
}
