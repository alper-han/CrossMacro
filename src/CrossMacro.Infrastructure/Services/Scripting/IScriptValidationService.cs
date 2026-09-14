
namespace CrossMacro.Infrastructure.Services.Scripting;

public interface IScriptValidationService
{
    public IReadOnlyList<ScriptValidationDiagnostic> Validate(IReadOnlyList<RunScriptStep> steps);
}
