
namespace CrossMacro.Core.Services;

public sealed class ScriptBlockStructureValidationResult(IReadOnlyList<string> errors)
{
    public bool IsValid => Errors.Count is 0;

    public IReadOnlyList<string> Errors { get; } = errors ?? throw new ArgumentNullException(nameof(errors));
}
