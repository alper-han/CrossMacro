
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Owns validation of script steps at the playback boundary.
/// </summary>
public sealed class PlaybackScriptValidator(IKeyCodeMapper keyCodeMapper, IScriptValidationService? validationService = null)
{
    private readonly IScriptValidationService _validationService = validationService ?? new ScriptValidationService(keyCodeMapper);

    public string? Validate(MacroSequence macro)
    {
        ArgumentNullException.ThrowIfNull(macro);
        var scriptSteps = macro.ScriptSteps
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .Select((step, index) => new RunScriptStep(step, SourceIndex: index))
            .ToList();
        if (scriptSteps.Count is 0)
        {
            return null;
        }

        var diagnostics = _validationService.Validate(scriptSteps);
        var diagnostic = diagnostics.Count > 0 ? diagnostics[0] : null;
        return diagnostic is null ? null : $"Macro script steps are invalid: {diagnostic.Message}";
    }
}
