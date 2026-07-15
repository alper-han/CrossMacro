
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Validates EditorAction instances with comprehensive rule checking.
/// </summary>
internal sealed class EditorActionProjectionValidator : IEditorActionValidator
{
    private readonly IEditorActionConverter _validationConverter;
    private readonly IScriptValidationService? _scriptValidationService;

    public EditorActionProjectionValidator(IEditorActionConverter validationConverter, IScriptValidationService? scriptValidationService = null)
    {
        _validationConverter = validationConverter ?? throw new ArgumentNullException(nameof(validationConverter));
        _scriptValidationService = scriptValidationService;
    }

    /// <inheritdoc/>
    public (bool IsValid, string? Error) Validate(EditorAction action)
    {
        if (action is null)
            return (false, ValidationMessages.ActionCannotBeNull);

        return action.Type switch
        {
            EditorActionType.Delay => ValidateDelay(action),
            EditorActionType.KeyPress or EditorActionType.KeyDown or EditorActionType.KeyUp => ValidateKeyAction(action),
            EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal => ValidateScroll(action),
            EditorActionType.MouseMove => ValidateMouseMove(action),
            EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp => ValidateMouseButton(action),
            EditorActionType.TextInput => ValidateTextInput(action),
            EditorActionType.SetVariable
                or EditorActionType.IncrementVariable
                or EditorActionType.DecrementVariable
                or EditorActionType.RepeatBlockStart
                or EditorActionType.IfBlockStart
                or EditorActionType.WhileBlockStart
                or EditorActionType.ForBlockStart => ValidateActionPayload(action),
            EditorActionType.RawScriptStep => ValidateRawScriptStep(action),
            EditorActionType.PixelColor => ValidatePixelColor(action),
            EditorActionType.WaitColor => ValidateWaitColor(action),
            EditorActionType.PixelSearch => ValidatePixelSearch(action),
            EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage => ValidateImageSearch(action),
            EditorActionType.ClipboardGet => ValidateClipboardGet(action),
            EditorActionType.ClipboardSet => ValidateClipboardSet(action),
            EditorActionType.ShellCommand => ValidateShellCommand(action),
            EditorActionType.Screenshot => ValidateScreenshot(action),
            EditorActionType.WindowCommand => ValidateWindowCommand(action),
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
        var errors = new List<string>();
        int index = 0;

        foreach (var action in actionList)
        {
            // Validate individual action
            var (isValid, error) = Validate(action);
            if (!isValid && error is not null)
            {
                errors.Add($"Action {index + 1} ({action.Type}): {error}");
            }

            index++;
        }

        var structureValidation = ScriptBlockStructureValidator.Validate(actionList);
        if (!structureValidation.IsValid)
        {
            errors.AddRange(structureValidation.Errors);
        }

        if (errors.Count is 0 && RequiresScriptBackedCompilation(actionList))
        {
            ValidateScriptCompilation(actionList, errors);
        }

        return (errors.Count is 0, errors);
    }

    public (bool IsValid, List<string> Errors) ValidateEditorFields(IEnumerable<EditorAction> actions)
    {
        var actionList = actions.ToList();
        var errors = new List<string>();
        for (var index = 0; index < actionList.Count; index++)
        {
            var (isValid, error) = Validate(actionList[index]);
            if (!isValid && error is not null)
            {
                errors.Add($"Action {index + 1} ({actionList[index].Type}): {error}");
            }
        }

        var structureValidation = ScriptBlockStructureValidator.Validate(actionList);
        if (!structureValidation.IsValid)
        {
            errors.AddRange(structureValidation.Errors);
        }

        return (errors.Count is 0, errors);
    }

    private static (bool IsValid, string? Error) ValidateDelay(EditorAction action)
    {
        if (action.UseRandomDelay)
        {
            if (action.RandomDelayMinMs < 0 || action.RandomDelayMaxMs < 0)
                return (false, ValidationMessages.DelayMustBeNonNegative);

            if (action.RandomDelayMaxMs < action.RandomDelayMinMs)
                return (false, ValidationMessages.RandomDelayBoundsInvalid);

            if (action.RandomDelayMinMs is 0 && action.RandomDelayMaxMs is 0)
                return (false, ValidationMessages.DelayMustBePositive);

            if (action.RandomDelayMaxMs > EditorActionValidationLimits.MaxDelayMs)
                return (false, ValidationMessages.DelayTooLong);

            return (true, null);
        }

        if (action.DelayMs < 0)
            return (false, ValidationMessages.DelayMustBeNonNegative);

        if (action.DelayMs is 0)
            return (false, ValidationMessages.DelayMustBePositive);

        if (action.DelayMs > EditorActionValidationLimits.MaxDelayMs)
            return (false, ValidationMessages.DelayTooLong);

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateKeyAction(EditorAction action)
    {
        if (action.KeyCode <= 0)
            return (false, ValidationMessages.KeyCodeMustBePositive);

        if (action.KeyCode > EditorActionValidationLimits.MaxKeyCode)
            return (false, ValidationMessages.KeyCodeInvalid);

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateScroll(EditorAction action)
    {
        if (action.ScrollAmount is 0)
            return (false, ValidationMessages.ScrollAmountCannotBeZero);

        if (Math.Abs(action.ScrollAmount) > EditorActionValidationLimits.MaxScrollAmount)
            return (false, ValidationMessages.ScrollAmountTooLarge);

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateMouseMove(EditorAction action)
    {
        return ValidateCoordinateBounds(action, requireRelativeNonZero: true);
    }

    private static (bool IsValid, string? Error) ValidateMouseButton(EditorAction action)
    {
        if (!Enum.IsDefined(typeof(MouseButton), action.Button))
            return (false, ValidationMessages.InvalidMouseButton);

        if (action.Button is MouseButton.ScrollUp or MouseButton.ScrollDown
            or MouseButton.ScrollLeft or MouseButton.ScrollRight)
        {
            return (false, ValidationMessages.UseScrollActionForScrollButtons);
        }

        if (action.UseCurrentPosition && action.IsAbsolute)
            return (false, ValidationMessages.CurrentPositionClickMustNotUseCoordinates);

        return ValidateCoordinateBounds(action, requireRelativeNonZero: false);
    }

    private static (bool IsValid, string? Error) ValidateCoordinateBounds(EditorAction action, bool requireRelativeNonZero)
    {
        if (action.IsAbsolute)
        {
            if (action.X < 0 || action.Y < 0)
                return (false, ValidationMessages.AbsoluteCoordsMustBeNonNegative);

            if (action.X > EditorActionValidationLimits.MaxAbsoluteCoordinate
                || action.Y > EditorActionValidationLimits.MaxAbsoluteCoordinate)
            {
                return (false, ValidationMessages.CoordsExceedMaximum);
            }
        }
        else
        {
            if (requireRelativeNonZero && action.X is 0 && action.Y is 0)
                return (false, ValidationMessages.RelativeMoveMustHaveValue);

            if (Math.Abs(action.X) > EditorActionValidationLimits.MaxRelativeCoordinateDelta
                || Math.Abs(action.Y) > EditorActionValidationLimits.MaxRelativeCoordinateDelta)
            {
                return (false, ValidationMessages.RelativeMoveTooLarge);
            }
        }

        return (true, null);
    }

    private static bool IsCurrentPositionMouseButtonAction(EditorAction action)
    {
        return action.Type is EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp
            && action.UseCurrentPosition;
    }

    private static bool UsesCoordinateMode(EditorActionType actionType)
    {
        return actionType is
            EditorActionType.MouseMove or
            EditorActionType.MouseClick or
            EditorActionType.MouseDown or
            EditorActionType.MouseUp;
    }

    private static (bool IsValid, string? Error) ValidateTextInput(EditorAction action)
    {
        if (string.IsNullOrEmpty(action.Text))
            return (false, ValidationMessages.TextInputRequired);

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateClipboardGet(EditorAction action)
    {
        return EditorActionScriptTokens.IsValidVariableName(action.ScriptVariableName)
            ? (true, null)
            : (false, "Clipboard destination variable name is invalid. Allowed: letters, digits, underscore; cannot start with digit.");
    }

    private static (bool IsValid, string? Error) ValidateClipboardSet(EditorAction action)
    {
        return string.IsNullOrEmpty(action.Text)
            ? (false, "Clipboard text cannot be empty.")
            : (true, null);
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
            && !RunScriptPlatformSyntax.IsScreenshotStep(text))
        {
            return (true, null);
        }

        try
        {
            _validationConverter.ToMacroSequence(new[] { action }, "Validation", isAbsolute: false);
            return (true, null);
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static (bool IsValid, string? Error) ValidateShellCommand(EditorAction action)
    {
        if (string.IsNullOrWhiteSpace(action.ShellCommand))
        {
            return (false, "Shell command cannot be empty.");
        }

        if (!Enum.IsDefined(typeof(ShellCommandMode), action.ShellCommandMode))
        {
            return (false, "Shell command mode is invalid.");
        }

        if (action.ShellCommandMode is ShellCommandMode.ShellCapture or ShellCommandMode.ShellCaptureInput)
        {
            if (!IsValidShellCaptureTarget(action.ShellExitCodeVariableName)
                || !IsValidShellCaptureTarget(action.ShellStandardOutputVariableName)
                || !IsValidShellCaptureTarget(action.ShellStandardErrorVariableName))
            {
                return (false, "Shell capture targets must be valid variable names, or '_' to ignore a stream.");
            }
        }

        if (action.ShellRetries < 0 || action.ShellRetries > 10_000)
        {
            return (false, "Shell retries must be between 0 and 10000.");
        }

        if (action.ShellBackoffMs < 0)
        {
            return (false, "Shell backoff_ms must be non-negative.");
        }

        if (action.ShellTimeoutMs < 0)
        {
            return (false, "Shell timeout_ms must be non-negative.");
        }

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateScreenshot(EditorAction action)
    {
        if (string.IsNullOrWhiteSpace(action.ScreenshotOutputPath) && !action.ScreenshotCopyToClipboard)
        {
            return (false, "Screenshot requires an output path or clipboard destination.");
        }

        if (!action.ScreenshotUseRegion)
        {
            return (true, null);
        }

        if (!IsIntegerOrVariable(action.ScreenshotRegionX) || !IsIntegerOrVariable(action.ScreenshotRegionY))
        {
            return (false, "Screenshot region x/y must be integers or variables.");
        }

        if (!IsPositiveIntegerOrVariable(action.ScreenshotRegionWidth) || !IsPositiveIntegerOrVariable(action.ScreenshotRegionHeight))
        {
            return (false, "Screenshot region width/height must be positive integers or variables.");
        }

        if (int.TryParse(action.ScreenshotRegionX, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
            && int.TryParse(action.ScreenshotRegionY, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            && int.TryParse(action.ScreenshotRegionWidth, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
            && int.TryParse(action.ScreenshotRegionHeight, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
            && !HasCheckedRegionEndpoints(x, y, width, height))
        {
            return (false, "Screenshot region endpoint exceeds the supported screen coordinate range.");
        }

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateWindowCommand(EditorAction action)
    {
        if (!Enum.IsDefined(typeof(WindowCommandMode), action.WindowCommandMode))
        {
            return (false, "Window command mode is invalid.");
        }

        var error = RunScriptWindowExecutor.Validate(EditorActionConverter.BuildWindowStep(action));
        return error is null ? (true, null) : (false, error);
    }

    private static bool IsIntegerOrVariable(string token)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            || (token.StartsWith("$", StringComparison.Ordinal) && EditorActionScriptTokens.IsValidVariableName(token));
    }

    private static bool HasCheckedRegionEndpoints(int left, int top, int width, int height)
    {
        try
        {
            _ = checked(left + width);
            _ = checked(top + height);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool IsPositiveIntegerOrVariable(string token)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value > 0
            : token.StartsWith("$", StringComparison.Ordinal) && EditorActionScriptTokens.IsValidVariableName(token);
    }

    private static bool IsValidShellCaptureTarget(string target)
    {
        return target is "_" || EditorActionScriptTokens.IsValidVariableName(target);
    }

    private static (bool IsValid, string? Error) ValidateActionPayload(EditorAction action)
    {
        if (action.PreferLegacyScriptText && !string.IsNullOrWhiteSpace(action.Text))
        {
            return (true, null);
        }

        return action.Type switch
        {
            EditorActionType.SetVariable => ValidateSetVariable(action),
            EditorActionType.IncrementVariable or EditorActionType.DecrementVariable => ValidateIncDec(action),
            EditorActionType.RepeatBlockStart => ValidateRepeat(action),
            EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart => ValidateCondition(action),
            EditorActionType.ForBlockStart => ValidateFor(action),
            _ => (false, ValidationMessages.ActionPayloadRequired),
        };
    }

    private static (bool IsValid, string? Error) ValidateSetVariable(EditorAction action)
    {
        if (!EditorActionScriptTokens.IsValidVariableName(action.ScriptVariableName))
        {
            return (false, "Variable name is invalid. Allowed: letters, digits, underscore; cannot start with digit.");
        }

        return action.ScriptValueType switch
        {
            ScriptValueType.Number => int.TryParse(action.ScriptValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                ? (true, null)
                : (false, "Set value must be a valid integer."),
            ScriptValueType.Boolean => bool.TryParse(action.ScriptValue, out _)
                ? (true, null)
                : (false, "Set value must be true or false."),
            ScriptValueType.Text => string.IsNullOrWhiteSpace(action.ScriptValue)
                ? (false, "Set value cannot be empty.")
                : (true, null),
            ScriptValueType.VariableReference => EditorActionScriptTokens.IsValidVariableName(action.ScriptValue)
                ? (true, null)
                : (false, "Referenced variable name is invalid."),
            _ => (false, ValidationMessages.ActionPayloadRequired),
        };
    }

    private static (bool IsValid, string? Error) ValidateIncDec(EditorAction action)
    {
        if (!EditorActionScriptTokens.IsValidVariableName(action.ScriptVariableName))
        {
            return (false, "Variable name is invalid. Allowed: letters, digits, underscore; cannot start with digit.");
        }

        if (!EditorActionScriptTokens.ValidateNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue))
        {
            if (action.ScriptNumericSourceType is ScriptNumericSourceType.VariableReference)
            {
                return (false, "Amount variable reference must be a variable name (example: step or $step), not a number literal.");
            }

            return (false, "Amount must be an integer or a valid variable reference.");
        }

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateRepeat(EditorAction action)
    {
        if (!EditorActionScriptTokens.ValidateNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue))
        {
            if (action.ScriptNumericSourceType is ScriptNumericSourceType.VariableReference)
            {
                return (false, "Repeat variable reference must be a variable name (example: count or $count), not a number literal.");
            }

            return (false, "Repeat count must be an integer or a valid variable reference.");
        }

        if (action.ScriptNumericSourceType is ScriptNumericSourceType.Number
&& int.TryParse(action.ScriptNumericValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var repeatCount)
&& repeatCount < 0)
        {
            return (false, "Repeat count must be >= 0.");
        }

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateCondition(EditorAction action)
    {
        if (!EditorActionScriptTokens.ValidateOperandToken(action.ScriptLeftOperandType, action.ScriptLeftOperand))
        {
            return (false, "Left operand is invalid for selected type.");
        }

        if (!EditorActionScriptTokens.ValidateOperandToken(action.ScriptRightOperandType, action.ScriptRightOperand))
        {
            return (false, "Right operand is invalid for selected type.");
        }

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateFor(EditorAction action)
    {
        if (!EditorActionScriptTokens.IsValidVariableName(action.ForVariableName))
        {
            return (false, "For-loop variable name is invalid.");
        }

        if (!EditorActionScriptTokens.ValidateNumericToken(action.ForStartType, action.ForStartValue))
        {
            if (action.ForStartType is ScriptNumericSourceType.VariableReference)
            {
                return (false, "For start variable reference must be a variable name (example: start or $start), not a number literal.");
            }

            return (false, "For start must be an integer or a valid variable reference.");
        }

        if (!EditorActionScriptTokens.ValidateNumericToken(action.ForEndType, action.ForEndValue))
        {
            if (action.ForEndType is ScriptNumericSourceType.VariableReference)
            {
                return (false, "For end variable reference must be a variable name (example: finish or $finish), not a number literal.");
            }

            return (false, "For end must be an integer or a valid variable reference.");
        }

        if (action.ForHasStep && !EditorActionScriptTokens.ValidateNumericToken(action.ForStepType, action.ForStepValue))
        {
            if (action.ForStepType is ScriptNumericSourceType.VariableReference)
            {
                return (false, "For step variable reference must be a variable name (example: step or $step), not a number literal.");
            }

            return (false, "For step must be an integer or a valid variable reference.");
        }

        if (action.ForHasStep
&& action.ForStepType is ScriptNumericSourceType.Number
&& int.TryParse(action.ForStepValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericStep)
&& numericStep is 0)
        {
            return (false, "For step cannot be 0.");
        }

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidatePixelColor(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);

        if (payload.IsAbsolute && (payload.ScreenX < 0 || payload.ScreenY < 0))
        {
            return (false, "Pixel color coordinates must be non-negative.");
        }

        if (!payload.HasValidColorVariableName())
        {
            return (false, "Pixel color output variable name is invalid.");
        }

        if (payload.ScreenTimeoutMs < 0)
        {
            return (false, "Pixel color timeout must be >= 0.");
        }

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateWaitColor(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);

        if (payload.ScreenX < 0 || payload.ScreenY < 0)
        {
            return (false, "Wait color coordinates must be non-negative.");
        }

        if (!payload.HasValidTargetColor())
        {
            return payload.ScreenTargetColorSource is EditorActionScreenTargetColorSource.Variable
                ? (false, "Wait color target variable name is invalid.")
                : (false, "Wait color target must be 6 hexadecimal RGB characters.");
        }

        if (payload.ScreenTimeoutMs < 0)
        {
            return (false, "Wait color timeout must be non-negative.");
        }

        if (!payload.HasValidColorVariableName())
        {
            return (false, "Wait color result variable name is invalid.");
        }

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidatePixelSearch(EditorAction action)
    {
        var payload = GetScreenReadingPayload(action);

        if (!payload.HasPositiveSearchRegion())
        {
            return (false, "Pixel search region size must be positive.");
        }

        if (!HasCheckedRegionEndpoints(payload.ScreenLeft, payload.ScreenTop, payload.ScreenWidth, payload.ScreenHeight))
        {
            return (false, "Pixel search region endpoint exceeds the supported screen coordinate range.");
        }

        if (!payload.HasValidTargetColor())
        {
            return payload.ScreenTargetColorSource is EditorActionScreenTargetColorSource.Variable
                ? (false, "Pixel search target variable name is invalid.")
                : (false, "Pixel search target must be 6 hexadecimal RGB characters.");
        }

        if (!payload.HasValidTolerance())
        {
            return (false, "Pixel search tolerance must be between 0 and 255.");
        }

        if (payload.ScreenTimeoutMs < 0)
        {
            return (false, "Pixel search timeout must be >= 0.");
        }

        if (!payload.HasValidFoundVariableName() || !payload.HasValidFoundCoordinateVariableNames())
        {
            return (false, "Pixel search output variable names are invalid.");
        }

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateImageSearch(EditorAction action)
    {
        if (action.ScreenWidth <= 0 || action.ScreenHeight <= 0)
        {
            return (false, "Image search region size must be positive.");
        }

        if (!HasCheckedRegionEndpoints(action.ScreenLeft, action.ScreenTop, action.ScreenWidth, action.ScreenHeight))
        {
            return (false, "Image search region endpoint exceeds the supported screen coordinate range.");
        }

        if (!EditorActionScriptTokens.IsValidVariableName(action.ImageAssetName))
        {
            return (false, "Image search asset name is invalid.");
        }

        if (!EditorActionScriptTokens.IsValidVariableName(action.ScreenFoundVariableName)
            || !EditorActionScriptTokens.IsValidVariableName(action.ScreenFoundXVariableName)
            || !EditorActionScriptTokens.IsValidVariableName(action.ScreenFoundYVariableName))
        {
            return (false, "Image search output variable names are invalid.");
        }

        if (!double.IsFinite(action.ImageSearchSimilarity) || action.ImageSearchSimilarity is < 0.0 or > 1.0)
        {
            return (false, "Image search similarity must be between 0.0 and 1.0.");
        }

        if (action.ImageSearchDownsample < 1)
        {
            return (false, "Image search downsample must be >= 1.");
        }

        if (!Enum.IsDefined(action.ImageSearchMatchMode))
        {
            return (false, "Image search match mode is invalid.");
        }

        if (action.ScreenTimeoutMs < 0)
        {
            return (false, "Image search timeout must be >= 0.");
        }

        if (action.Type is EditorActionType.ImageClick
&& action.Button is not (MouseButton.Left or MouseButton.Right or MouseButton.Middle))
        {
            return (false, "Image click button must be left, right, or middle.");
        }

        return (true, null);
    }

    private static EditorActionScreenReadingPayload GetScreenReadingPayload(EditorAction action)
    {
        if (!action.TryGetScreenReadingPayload(out var payload))
        {
            throw new InvalidOperationException("Action type does not contain a screen-reading payload.");
        }

        return payload;
    }

    private void ValidateScriptCompilation(IReadOnlyList<EditorAction> actions, List<string> errors)
    {
        var scriptSteps = actions
            .Where(action => action.Type is EditorActionType.RawScriptStep && !string.IsNullOrWhiteSpace(action.Text))
            .Select((action, index) => new RunScriptStep(action.Text, SourceIndex: index))
            .ToList();
        if (scriptSteps.Count > 0 && _scriptValidationService is not null)
        {
            foreach (var diagnostic in _scriptValidationService.Validate(scriptSteps))
            {
                errors.Add($"Script: {diagnostic.Message}");
            }

            return;
        }

        try
        {
            var firstCoordinateAction = actions.FirstOrDefault(action =>
                UsesCoordinateMode(action.Type) && !IsCurrentPositionMouseButtonAction(action));
            var isAbsolute = firstCoordinateAction?.IsAbsolute ?? false;
            var skipInitialZeroZero = actions.Any(IsCurrentPositionMouseButtonAction);

            _validationConverter.ToMacroSequence(actions, "Validation", isAbsolute, skipInitialZeroZero);
        }
        catch (InvalidOperationException ex)
        {
            errors.Add($"Script: {ex.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"Script: {ex.Message}");
        }
    }

    private static bool RequiresScriptBackedCompilation(IReadOnlyList<EditorAction> actions)
    {
        var hasFlowControlScriptActions = actions.Any(action => EditorActionScriptClassifier.IsScriptFlowControlAction(action.Type));
        var hasStateScriptActions = actions.Any(action => EditorActionScriptClassifier.IsScriptStateAction(action.Type));
        var hasOpaqueScriptActions = actions.Any(action => EditorActionScriptClassifier.IsOpaqueScriptAction(action.Type));
        var hasRuntimeEventActions = actions.Any(action => EditorActionScriptClassifier.IsRuntimeEventAction(action.Type));
        return hasFlowControlScriptActions
            || hasOpaqueScriptActions
            || (hasStateScriptActions && !hasRuntimeEventActions);
    }
}
