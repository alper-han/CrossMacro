using System.Collections.Generic;

namespace CrossMacro.Infrastructure.Services;

public interface IScriptValidationService
{
    IReadOnlyList<ScriptValidationDiagnostic> Validate(IReadOnlyList<RunScriptStep> steps);
}
