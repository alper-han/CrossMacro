
namespace CrossMacro.Core.Services;

public interface IPlaybackValidator
{
    public PlaybackValidationResult Validate(MacroSequence macro);
}
