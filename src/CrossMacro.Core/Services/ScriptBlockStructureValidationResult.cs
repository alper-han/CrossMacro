
namespace CrossMacro.Core.Services;

public sealed class ScriptBlockStructureValidationResult
{
    public ScriptBlockStructureValidationResult(IReadOnlyList<string> errors)
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    public bool IsValid => Errors.Count is 0;

    public IReadOnlyList<string> Errors { get; }
}
