
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Maps pixels of the logical (upright) image to the buffer that holds it in a <see cref="WaylandBufferTransform"/>.
/// </summary>
internal static class WaylandBufferOrientation
{
    private const int FlipBit = 4;
    private const int RotationMask = 3;

    public static bool TryParse(uint value, out WaylandBufferTransform transform)
    {
        var valid = value <= (uint)WaylandBufferTransform.FlippedRotate270;
        transform = valid ? (WaylandBufferTransform)value : WaylandBufferTransform.Normal;
        return valid;
    }

    /// <summary>
    /// The wlr-screencopy <c>y_invert</c> flag: the buffer is the logical image mirrored top to bottom.
    /// </summary>
    public static WaylandBufferTransform FromYInvert(bool yInverted) =>
        yInverted ? WaylandBufferTransform.FlippedRotate180 : WaylandBufferTransform.Normal;

    public static bool SwapsAxes(WaylandBufferTransform transform) => ((int)transform & 1) is not 0;

    /// <summary>
    /// Size of the logical image whose transformed copy is a buffer of <paramref name="bufferWidth"/> x <paramref name="bufferHeight"/>.
    /// </summary>
    public static (int Width, int Height) GetLogicalSize(WaylandBufferTransform transform, int bufferWidth, int bufferHeight) =>
        SwapsAxes(transform) ? (bufferHeight, bufferWidth) : (bufferWidth, bufferHeight);

    /// <summary>
    /// Position in the buffer of the pixel at (<paramref name="x"/>, <paramref name="y"/>) of the
    /// <paramref name="width"/> x <paramref name="height"/> logical image.
    /// </summary>
    public static (int X, int Y) ToBuffer(WaylandBufferTransform transform, int width, int height, int x, int y)
    {
        var bits = (int)transform;
        if ((bits & FlipBit) is not 0)
        {
            x = width - 1 - x;
        }

        return (bits & RotationMask) switch
        {
            1 => (y, width - 1 - x),
            2 => (width - 1 - x, height - 1 - y),
            3 => (height - 1 - y, x),
            _ => (x, y),
        };
    }
}
