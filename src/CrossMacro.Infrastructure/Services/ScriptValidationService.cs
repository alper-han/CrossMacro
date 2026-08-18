
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Infrastructure boundary for script syntax and runtime-command validation.
/// Compilation remains owned by RunScriptCompiler; callers only consume diagnostics.
/// </summary>
public sealed class ScriptValidationService(IKeyCodeMapper keyCodeMapper) : IScriptValidationService
{
    private readonly RunScriptCompiler _compiler = new RunScriptCompiler(keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper)));

    public IReadOnlyList<ScriptValidationDiagnostic> Validate(IReadOnlyList<RunScriptStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count is 0)
        {
            return [];
        }

        var result = _compiler.Compile(steps);
        return result.Success
            ? []
            :
            [
                new ScriptValidationDiagnostic(
                    ScriptValidationCategory.Compilation,
                    result.ErrorMessage,
                    steps[0].SourceLineNumber,
                    steps[0].SourceIndex),
            ];
    }
}
