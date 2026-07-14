using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Owns validation of script steps at the playback boundary.
/// </summary>
public sealed class PlaybackScriptValidator
{
    private readonly IScriptValidationService _validationService;

    public PlaybackScriptValidator(IKeyCodeMapper keyCodeMapper, IScriptValidationService? validationService = null)
    {
        _validationService = validationService ?? new ScriptValidationService(keyCodeMapper);
    }

    public string? Validate(MacroSequence macro)
    {
        var scriptSteps = macro.ScriptSteps
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .Select((step, index) => new RunScriptStep(step, SourceIndex: index))
            .ToList();
        if (scriptSteps.Count == 0)
        {
            return null;
        }

        var diagnostic = _validationService.Validate(scriptSteps).FirstOrDefault();
        return diagnostic is null ? null : $"Macro script steps are invalid: {diagnostic.Message}";
    }
}
