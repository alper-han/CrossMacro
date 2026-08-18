
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriLayoutDto
{
    [JsonPropertyName("pos_in_scrolling_layout")]
    public IReadOnlyList<double>? PosInScrollingLayout { get; set; }

    [JsonPropertyName("tile_size")]
    public IReadOnlyList<double>? TileSize { get; set; }

    [JsonPropertyName("window_size")]
    public IReadOnlyList<double>? WindowSize { get; set; }

    [JsonPropertyName("tile_pos_in_workspace_view")]
    public IReadOnlyList<double>? TilePosInWorkspaceView { get; set; }

    [JsonPropertyName("window_offset_in_tile")]
    public IReadOnlyList<double>? WindowOffsetInTile { get; set; }
}
