
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Infrastructure boundary for script syntax and runtime-command validation.
/// Compilation remains owned by RunScriptCompiler; callers only consume diagnostics.
/// </summary>
public sealed class ScriptValidationService : IScriptValidationService
{
    private readonly RunScriptCompiler _compiler;

    public ScriptValidationService(IKeyCodeMapper keyCodeMapper)
    {
        _compiler = new RunScriptCompiler(keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper)));
    }

    public IReadOnlyList<ScriptValidationDiagnostic> Validate(IReadOnlyList<RunScriptStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count is 0)
        {
            return Array.Empty<ScriptValidationDiagnostic>();
        }

        var result = _compiler.Compile(steps);
        return result.Success
            ? Array.Empty<ScriptValidationDiagnostic>()
            : new[]
            {
                new ScriptValidationDiagnostic(
                    ScriptValidationCategory.Compilation,
                    result.ErrorMessage,
                    steps.FirstOrDefault()?.SourceLineNumber,
                    steps.FirstOrDefault()?.SourceIndex ?? 0),
            };
    }
}
