using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

public interface IPlaybackValidator
{
    ValidationResult Validate(MacroSequence macro);
}
