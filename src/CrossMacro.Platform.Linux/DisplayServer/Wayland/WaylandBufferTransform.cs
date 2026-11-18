
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// <c>wl_output.transform</c>: how buffer contents are rotated and mirrored relative to the logical image.
/// Rotations are counter-clockwise; the flipped variants mirror around the vertical axis first and then rotate.
/// </summary>
public enum WaylandBufferTransform
{
    Normal = 0,
    Rotate90 = 1,
    Rotate180 = 2,
    Rotate270 = 3,
    Flipped = 4,
    FlippedRotate90 = 5,
    FlippedRotate180 = 6,
    FlippedRotate270 = 7,
}
