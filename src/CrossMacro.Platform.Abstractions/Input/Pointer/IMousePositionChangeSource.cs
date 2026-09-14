namespace CrossMacro.Platform.Abstractions.Input.Pointer;

public interface IMousePositionChangeSource
{
    public event EventHandler<MousePositionChangedEventArgs>? PositionChanged;
}
