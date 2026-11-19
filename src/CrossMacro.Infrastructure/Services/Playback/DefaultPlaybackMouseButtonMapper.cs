
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Default implementation of IPlaybackMouseButtonMapper.
/// Maps MacroMouseButton enum to Linux kernel-style button codes. Only physical buttons map; scroll
/// directions are emitted as wheel events and <see cref="MacroMouseButton.None"/> carries no button, so
/// mapping them would silently turn a malformed event into a real left click.
/// </summary>
public class DefaultPlaybackMouseButtonMapper : IPlaybackMouseButtonMapper
{
    public int Map(MacroMouseButton button)
    {
        return button switch
        {
            MacroMouseButton.Left => MouseButtonCode.Left,
            MacroMouseButton.Right => MouseButtonCode.Right,
            MacroMouseButton.Middle => MouseButtonCode.Middle,
            MacroMouseButton.Side1 => MouseButtonCode.Side1,
            MacroMouseButton.Side2 => MouseButtonCode.Side2,
            MacroMouseButton.None
                or MacroMouseButton.ScrollUp
                or MacroMouseButton.ScrollDown
                or MacroMouseButton.ScrollLeft
                or MacroMouseButton.ScrollRight
                => throw new ArgumentOutOfRangeException(nameof(button), button, "Mouse button is not clickable."),
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unknown mouse button."),
        };
    }
}
