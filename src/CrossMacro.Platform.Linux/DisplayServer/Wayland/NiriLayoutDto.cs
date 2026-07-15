
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriLayoutDto
{
    [JsonPropertyName("pos_in_scrolling_layout")]
    public double[]? PosInScrollingLayout { get; set; }

    [JsonPropertyName("tile_size")]
    public double[]? TileSize { get; set; }

    [JsonPropertyName("window_size")]
    public double[]? WindowSize { get; set; }

    [JsonPropertyName("tile_pos_in_workspace_view")]
    public double[]? TilePosInWorkspaceView { get; set; }

    [JsonPropertyName("window_offset_in_tile")]
    public double[]? WindowOffsetInTile { get; set; }
}
