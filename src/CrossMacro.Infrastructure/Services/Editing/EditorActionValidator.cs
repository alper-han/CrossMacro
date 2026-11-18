
namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>
/// Validates EditorAction instances with comprehensive rule checking. Per-action rules live in the
/// family validators; this type dispatches to them and validates the action list as a script.
/// </summary>
public sealed class EditorActionValidator(IEditorActionConverter validationConverter, IScriptValidationService? scriptValidationService = null) : IEditorActionValidator
{
    private readonly IEditorActionConverter _validationConverter = validationConverter ?? throw new ArgumentNullException(nameof(validationConverter));
    private readonly IScriptValidationService? _scriptValidationService = scriptValidationService;

    /// <inheritdoc/>
    public (bool IsValid, string? Error) Validate(EditorAction action)
    {
        if (action is null)
        {
            return (false, ValidationMessages.ActionCannotBeNull);
        }

        return action.Type switch
        {
            EditorActionType.Delay => EditorInputActionValidator.ValidateDelay(action),
            EditorActionType.KeyPress or EditorActionType.KeyDown or EditorActionType.KeyUp => EditorInputActionValidator.ValidateKeyAction(action),
            EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal => EditorInputActionValidator.ValidateScroll(action),
            EditorActionType.MouseMove => EditorInputActionValidator.ValidateMouseMove(action),
            EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp => EditorInputActionValidator.ValidateMouseButton(action),
            EditorActionType.TextInput => EditorInputActionValidator.ValidateTextInput(action),
            EditorActionType.SetVariable
                or EditorActionType.IncrementVariable
                or EditorActionType.DecrementVariable
                or EditorActionType.MultiplyVariable
                or EditorActionType.DivideVariable
                or EditorActionType.RepeatBlockStart
                or EditorActionType.IfBlockStart
                or EditorActionType.WhileBlockStart
                or EditorActionType.ForBlockStart => EditorScriptActionValidator.ValidateActionPayload(action),
            EditorActionType.MousePosition => EditorScriptActionValidator.ValidateMousePosition(action),
            EditorActionType.ClipboardGet => EditorScriptActionValidator.ValidateClipboardGet(action),
            EditorActionType.ClipboardSet => EditorScriptActionValidator.ValidateClipboardSet(action),
            EditorActionType.CopySelectionToVariable => EditorScriptActionValidator.ValidateCopySelectionToVariable(action),
            EditorActionType.RawScriptStep => ValidateRawScriptStep(action),
            EditorActionType.PixelColor => EditorScreenActionValidator.ValidatePixelColor(action),
            EditorActionType.WaitColor => EditorScreenActionValidator.ValidateWaitColor(action),
            EditorActionType.PixelSearch => EditorScreenActionValidator.ValidatePixelSearch(action),
            EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage => EditorScreenActionValidator.ValidateImageSearch(action),
            EditorActionType.Screenshot => EditorScreenActionValidator.ValidateScreenshot(action),
            EditorActionType.ShellCommand => EditorCommandActionValidator.ValidateShellCommand(action),
            EditorActionType.WindowCommand => EditorCommandActionValidator.ValidateWindowCommand(action),
            EditorActionType.ElseBlockStart
                or EditorActionType.BlockEnd
                or EditorActionType.Break
                or EditorActionType.Continue => (true, null),
            _ => (true, null),
        };
    }

    /// <inheritdoc/>
    public (bool IsValid, List<string> Errors) ValidateAll(IEnumerable<EditorAction> actions)
    {
        var actionList = actions.ToList();
        var (_, errors) = ValidateEditorFields(actionList);
        if (errors.Count is 0)
        {
            ValidateScriptCompilation(actionList, errors);
        }

        return (errors.Count is 0, errors);
    }

    private (bool IsValid, List<string> Errors) ValidateEditorFields(List<EditorAction> actionList)
    {
        var errors = new List<string>();
        for (var index = 0; index < actionList.Count; index++)
        {
            var (isValid, error) = Validate(actionList[index]);
            if (!isValid && error is not null)
            {
                var action = actionList[index];
                var actionType = action is null ? "null" : action.Type.ToString();
                errors.Add($"Action {(index + 1).ToString(CultureInfo.InvariantCulture)} ({actionType}): {error}");
            }
        }

        if (!actionList.Exists(static action => action is null))
        {
            var structureValidation = ScriptBlockStructureValidator.Validate(actionList);
            if (!structureValidation.IsValid)
            {
                errors.AddRange(structureValidation.Errors);
            }
        }

        return (errors.Count is 0, errors);
    }

    private (bool IsValid, string? Error) ValidateRawScriptStep(EditorAction action)
    {
        if (string.IsNullOrWhiteSpace(action.Text))
        {
            return (false, "Raw script step cannot be empty.");
        }

        var text = action.Text;
        if (!RunScriptSyntax.IsWindowStep(text)
            && !RunScriptSyntax.IsClipboardStep(text)
            && !RunScriptSyntax.IsShellStep(text)
            && !RunScriptSyntax.IsScreenReadingStep(text)
            && !RunScriptSyntax.IsMousePositionStep(text)
            && !RunScriptSyntax.IsScreenshotStep(text))
        {
            return (true, null);
        }

        if (_scriptValidationService is not null)
        {
            var diagnostics = _scriptValidationService.Validate([new RunScriptStep(text, SourceIndex: 0)]);
            return diagnostics.Count > 0 ? (false, diagnostics[0].Message) : (true, null);
        }

        try
        {
            _ = _validationConverter.ToMacroSequence([action], "Validation", isAbsolute: false);
            return (true, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return (false, ex.Message);
        }
    }

    private void ValidateScriptCompilation(IReadOnlyList<EditorAction> actions, List<string> errors)
    {
        if (_scriptValidationService is null)
        {
            if (RequiresScriptBackedCompilation(actions))
            {
                CompileForErrors(actions, errors);
            }

            return;
        }

        if (HasVariableCoordinateActions(actions))
        {
            CompileForErrors(actions, errors);
        }

        var scriptSteps = actions
            .Where(action => action.Type is EditorActionType.RawScriptStep && !string.IsNullOrWhiteSpace(action.Text))
            .Select((action, index) => new RunScriptStep(action.Text, SourceIndex: index))
            .ToList();
        foreach (var diagnostic in _scriptValidationService.Validate(scriptSteps))
        {
            errors.Add($"Script: {diagnostic.Message}");
        }
    }

    private void CompileForErrors(IReadOnlyList<EditorAction> actions, List<string> errors)
    {
        try
        {
            var firstCoordinateAction = actions.FirstOrDefault(EditorActionCoordinateRules.HasPositionCoordinates);
            var isAbsolute = firstCoordinateAction?.IsAbsolute ?? false;
            var skipInitialZeroZero = actions.Any(static action => EditorActionCoordinateRules.IsCurrentPositionMouseButton(action));

            _ = _validationConverter.ToMacroSequence(actions, "Validation", isAbsolute, skipInitialZeroZero);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add($"Script: {ex.Message}");
        }
    }

    private static bool HasVariableCoordinateActions(IReadOnlyList<EditorAction> actions)
    {
        return actions.Any(action => EditorActionCoordinateRules.HasPositionCoordinates(action) && action.HasVariableCoordinates);
    }

    private static bool RequiresScriptBackedCompilation(IReadOnlyList<EditorAction> actions)
    {
        var hasStateScriptActions = actions.Any(action => EditorActionScriptClassifier.IsScriptStateAction(action.Type));
        var hasRuntimeEventActions = actions.Any(action => EditorActionScriptClassifier.IsRuntimeEventAction(action.Type));
        return actions.Any(action => EditorActionScriptClassifier.IsScriptFlowControlAction(action.Type))
            || actions.Any(action => EditorActionScriptClassifier.IsOpaqueScriptAction(action.Type))
            || HasVariableCoordinateActions(actions)
            || (hasStateScriptActions && !hasRuntimeEventActions);
    }
}
