
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Default implementation of IPlaybackMouseButtonMapper.
/// Maps MacroMouseButton enum to Linux kernel-style button codes.
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
            _ => MouseButtonCode.Left,
        };
    }
}
