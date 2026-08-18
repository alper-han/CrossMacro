namespace CrossMacro.Platform.Abstractions;

public sealed class MousePositionChangedEventArgs(
    int x,
    int y,
    bool isDiscontinuity = false) : EventArgs
{
    public int X { get; } = x;

    public int Y { get; } = y;

    public bool IsDiscontinuity { get; } = isDiscontinuity;
}
