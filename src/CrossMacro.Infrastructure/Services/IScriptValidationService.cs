
namespace CrossMacro.Infrastructure.Services;

public interface IScriptValidationService
{
    public IReadOnlyList<ScriptValidationDiagnostic> Validate(IReadOnlyList<RunScriptStep> steps);
}
