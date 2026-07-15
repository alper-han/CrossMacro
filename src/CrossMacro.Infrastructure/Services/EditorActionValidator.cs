
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Compatibility façade combining editor field/projection validation with the
/// shared Infrastructure script-validation boundary.
/// </summary>
public sealed class EditorActionValidator : IEditorActionValidator
{
    private readonly EditorActionProjectionValidator _projectionValidator;
    private readonly EditorActionScriptValidationAdapter _scriptAdapter;
    private readonly IScriptValidationService? _scriptValidationService;

    public EditorActionValidator(IEditorActionConverter validationConverter, IScriptValidationService? scriptValidationService = null)
    {
        _scriptValidationService = scriptValidationService;
        _projectionValidator = new EditorActionProjectionValidator(validationConverter, scriptValidationService);
        _scriptAdapter = new EditorActionScriptValidationAdapter(validationConverter, scriptValidationService);
    }

    public (bool IsValid, string? Error) Validate(EditorAction action)
    {
        if (action is null)
        {
            return (false, ValidationMessages.ActionCannotBeNull);
        }

        return action.Type is EditorActionType.RawScriptStep
&& (RunScriptSyntax.IsWindowStep(action.Text)
                || RunScriptSyntax.IsClipboardStep(action.Text)
                || RunScriptSyntax.IsShellStep(action.Text)
                || RunScriptSyntax.IsScreenReadingStep(action.Text)
                || RunScriptPlatformSyntax.IsScreenshotStep(action.Text))
            ? _scriptAdapter.ValidateAction(action)
            : _projectionValidator.Validate(action);
    }

    public (bool IsValid, List<string> Errors) ValidateAll(IEnumerable<EditorAction> actions)
    {
        if (_scriptValidationService is null)
        {
            return _projectionValidator.ValidateAll(actions);
        }

        var actionList = actions.ToList();
        var editorResult = _projectionValidator.ValidateEditorFields(actionList);
        var errors = editorResult.Errors;
        if (errors.Count is 0)
        {
            errors.AddRange(_scriptAdapter.Validate(actionList));
        }

        return (errors.Count is 0, errors);
    }
}
