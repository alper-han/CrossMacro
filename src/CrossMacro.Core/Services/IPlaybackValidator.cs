using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

public interface IPlaybackValidator
{
    ValidationResult Validate(MacroSequence macro);
}

public sealed class ValidationResult
{
    public bool IsValid => Errors.Count is 0;
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];

    public void AddWarning(string message) => Warnings.Add(message);
    public void AddError(string message) => Errors.Add(message);
}
