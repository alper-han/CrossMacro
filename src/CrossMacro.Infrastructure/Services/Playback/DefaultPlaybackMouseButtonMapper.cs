using CrossMacro.Core.Models;
using CrossMacro.Core.Services.Playback;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Default implementation of IPlaybackMouseButtonMapper.
/// Maps MouseButton enum to Linux kernel-style button codes.
/// </summary>
public class DefaultPlaybackMouseButtonMapper : IPlaybackMouseButtonMapper
{
    public int Map(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => MouseButtonCode.Left,
            MouseButton.Right => MouseButtonCode.Right,
            MouseButton.Middle => MouseButtonCode.Middle,
            MouseButton.Side1 => MouseButtonCode.Side1,
            MouseButton.Side2 => MouseButtonCode.Side2,
            _ => MouseButtonCode.Left
        };
    }
}
