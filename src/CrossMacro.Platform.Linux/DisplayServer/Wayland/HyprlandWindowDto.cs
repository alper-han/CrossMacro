
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class HyprlandWindowDto
{
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("class")]
    public string? Class { get; set; }

    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("focusHistoryID")]
    public int FocusHistoryId { get; set; }

    [JsonPropertyName("fullscreen")]
    public int Fullscreen { get; set; }

    [JsonPropertyName("floating")]
    public bool Floating { get; set; }

    [JsonPropertyName("pinned")]
    public bool Pinned { get; set; }

    [JsonPropertyName("hidden")]
    public bool Hidden { get; set; }
    [JsonPropertyName("at")] public int[]? At { get; set; }
    [JsonPropertyName("size")] public int[]? Size { get; set; }

    [JsonPropertyName("workspace")]
    public HyprlandWorkspaceDto? Workspace { get; set; }
}
