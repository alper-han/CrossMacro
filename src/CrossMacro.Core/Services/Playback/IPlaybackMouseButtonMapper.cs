
namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Maps MacroMouseButton enum to platform-agnostic button codes.
/// </summary>
public interface IPlaybackMouseButtonMapper
{
    /// <summary>
    /// Map a MacroMouseButton enum value to its numeric code
    /// </summary>
    public int Map(MacroMouseButton button);
}
