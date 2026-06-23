using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Resources;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Validates EditorAction instances with comprehensive rule checking.
/// </summary>
public class EditorActionValidator : IEditorActionValidator
{
    private readonly IEditorActionConverter _validationConverter;

    public EditorActionValidator(IEditorActionConverter validationConverter)
    {
        _validationConverter = validationConverter ?? throw new ArgumentNullException(nameof(validationConverter));
    }
    
    /// <inheritdoc/>
    public (bool IsValid, string? Error) Validate(EditorAction action)
    {
        if (action == null)
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
            EditorActionType.RawScriptStep => string.IsNullOrWhiteSpace(action.Text)
                ? (false, "Raw script step cannot be empty.")
                : (true, null),
            EditorActionType.PixelColor => ValidatePixelColor(action),
            EditorActionType.WaitColor => ValidateWaitColor(action),
            EditorActionType.PixelSearch => ValidatePixelSearch(action),
            EditorActionType.ElseBlockStart
                or EditorActionType.BlockEnd
                or EditorActionType.Break
                or EditorActionType.Continue => (true, null),
            _ => (true, null)
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
            if (!isValid && error != null)
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

        if (errors.Count == 0 && RequiresScriptBackedCompilation(actionList))
        {
            ValidateScriptCompilation(actionList, errors);
        }
        
        return (errors.Count == 0, errors);
    }
    
    private static (bool IsValid, string? Error) ValidateDelay(EditorAction action)
    {
        if (action.UseRandomDelay)
        {
            if (action.RandomDelayMinMs < 0 || action.RandomDelayMaxMs < 0)
                return (false, ValidationMessages.DelayMustBeNonNegative);

            if (action.RandomDelayMaxMs < action.RandomDelayMinMs)
                return (false, ValidationMessages.RandomDelayBoundsInvalid);

            if (action.RandomDelayMinMs == 0 && action.RandomDelayMaxMs == 0)
                return (false, ValidationMessages.DelayMustBePositive);

            if (action.RandomDelayMaxMs > EditorActionValidationLimits.MaxDelayMs)
                return (false, ValidationMessages.DelayTooLong);

            return (true, null);
        }

        if (action.DelayMs < 0)
            return (false, ValidationMessages.DelayMustBeNonNegative);

        if (action.DelayMs == 0)
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
        if (action.ScrollAmount == 0)
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
            return (false, ValidationMessages.UseScrollActionForScrollButtons);

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
                return (false, ValidationMessages.CoordsExceedMaximum);
        }
        else
        {
            if (requireRelativeNonZero && action.X == 0 && action.Y == 0)
                return (false, ValidationMessages.RelativeMoveMustHaveValue);

            if (Math.Abs(action.X) > EditorActionValidationLimits.MaxRelativeCoordinateDelta
                || Math.Abs(action.Y) > EditorActionValidationLimits.MaxRelativeCoordinateDelta)
                return (false, ValidationMessages.RelativeMoveTooLarge);
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
            _ => (false, ValidationMessages.ActionPayloadRequired)
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
            _ => (false, ValidationMessages.ActionPayloadRequired)
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
            if (action.ScriptNumericSourceType == ScriptNumericSourceType.VariableReference)
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
            if (action.ScriptNumericSourceType == ScriptNumericSourceType.VariableReference)
            {
                return (false, "Repeat variable reference must be a variable name (example: count or $count), not a number literal.");
            }

            return (false, "Repeat count must be an integer or a valid variable reference.");
        }

        if (action.ScriptNumericSourceType == ScriptNumericSourceType.Number
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
            if (action.ForStartType == ScriptNumericSourceType.VariableReference)
            {
                return (false, "For start variable reference must be a variable name (example: start or $start), not a number literal.");
            }

            return (false, "For start must be an integer or a valid variable reference.");
        }

        if (!EditorActionScriptTokens.ValidateNumericToken(action.ForEndType, action.ForEndValue))
        {
            if (action.ForEndType == ScriptNumericSourceType.VariableReference)
            {
                return (false, "For end variable reference must be a variable name (example: finish or $finish), not a number literal.");
            }

            return (false, "For end must be an integer or a valid variable reference.");
        }

        if (action.ForHasStep && !EditorActionScriptTokens.ValidateNumericToken(action.ForStepType, action.ForStepValue))
        {
            if (action.ForStepType == ScriptNumericSourceType.VariableReference)
            {
                return (false, "For step variable reference must be a variable name (example: step or $step), not a number literal.");
            }

            return (false, "For step must be an integer or a valid variable reference.");
        }

        if (action.ForHasStep
            && action.ForStepType == ScriptNumericSourceType.Number
            && int.TryParse(action.ForStepValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericStep)
            && numericStep == 0)
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
            return payload.ScreenTargetColorSource == EditorActionScreenTargetColorSource.Variable
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

        if (payload.ScreenLeft < 0 || payload.ScreenTop < 0)
        {
            return (false, "Pixel search region origin must be non-negative.");
        }

        if (!payload.HasPositiveSearchRegion())
        {
            return (false, "Pixel search region size must be positive.");
        }

        if (!payload.HasValidTargetColor())
        {
            return payload.ScreenTargetColorSource == EditorActionScreenTargetColorSource.Variable
                ? (false, "Pixel search target variable name is invalid.")
                : (false, "Pixel search target must be 6 hexadecimal RGB characters.");
        }

        if (!payload.HasValidTolerance())
        {
            return (false, "Pixel search tolerance must be between 0 and 255.");
        }

        if (!payload.HasValidFoundVariableName() || !payload.HasValidFoundCoordinateVariableNames())
        {
            return (false, "Pixel search output variable names are invalid.");
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
