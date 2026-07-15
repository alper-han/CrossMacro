
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriWorkspacesData
{
    [JsonPropertyName("Workspaces")]
    public NiriWorkspaceDto[]? Workspaces { get; set; }
}
