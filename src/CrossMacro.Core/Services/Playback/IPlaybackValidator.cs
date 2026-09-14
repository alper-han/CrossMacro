
namespace CrossMacro.Core.Services.Playback;

public interface IPlaybackValidator
{
    public PlaybackValidationResult Validate(MacroSequence macro);
}
