namespace CrossMacro.Platform.Abstractions;

public interface IMousePositionChangeSource
{
    public event EventHandler<MousePositionChangedEventArgs>? PositionChanged;
}
