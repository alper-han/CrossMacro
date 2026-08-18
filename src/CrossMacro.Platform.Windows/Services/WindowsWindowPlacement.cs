namespace CrossMacro.Platform.Windows.Services;

/// <summary>
/// Pure placement geometry shared by move, resize and center operations.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct WindowsWindowPlacement(
    IntPtr Hwnd,
    RectStruct OuterBounds,
    RectStruct VisibleBounds)
{
    internal int LeftMargin => VisibleBounds.left - OuterBounds.left;
    internal int TopMargin => VisibleBounds.top - OuterBounds.top;
    internal int RightMargin => OuterBounds.right - VisibleBounds.right;
    internal int BottomMargin => OuterBounds.bottom - VisibleBounds.bottom;
    internal int HorizontalMargin => LeftMargin + RightMargin;
    internal int VerticalMargin => TopMargin + BottomMargin;
    internal int VisibleWidth => Math.Max(0, VisibleBounds.right - VisibleBounds.left);
    internal int VisibleHeight => Math.Max(0, VisibleBounds.bottom - VisibleBounds.top);
}
