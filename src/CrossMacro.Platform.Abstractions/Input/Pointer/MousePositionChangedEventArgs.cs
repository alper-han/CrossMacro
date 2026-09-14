namespace CrossMacro.Platform.Abstractions.Input.Pointer;

public sealed class MousePositionChangedEventArgs(
    int x,
    int y,
    bool isDiscontinuity = false) : EventArgs
{
    public int X { get; } = x;

    public int Y { get; } = y;

    public bool IsDiscontinuity { get; } = isDiscontinuity;
}
