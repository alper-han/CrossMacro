
namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Maps MouseButton enum to platform-agnostic button codes.
/// </summary>
public interface IPlaybackMouseButtonMapper
{
    /// <summary>
    /// Map a MouseButton enum value to its numeric code
    /// </summary>
    int Map(MouseButton button);
}
