using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Resources;
using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Validates EditorAction instances with comprehensive rule checking.
/// </summary>
public class EditorActionValidator : IEditorActionValidator
{
    private readonly IEditorActionConverter _validationConverter;

    /// <summary>
    /// Maximum allowed length for TextInput content.
    /// </summary>
    public const int MaxTextInputLength = EditorActionValidationLimits.MaxTextInputLength;

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
        bool? firstCoordinateMode = null;
        
        foreach (var action in actionList)
        {
            // Validate individual action
            var (isValid, error) = Validate(action);
            if (!isValid && error != null)
            {
                errors.Add($"Action {index + 1} ({action.Type}): {error}");
            }
            
            // Validate coordinate mode consistency
            if (UsesGlobalCoordinateMode(action))
            {
                if (firstCoordinateMode == null)
                {
                    firstCoordinateMode = action.IsAbsolute;
                }
                else if (firstCoordinateMode != action.IsAbsolute)
                {
                    errors.Add($"Action {index + 1}: Cannot mix Absolute and Relative coordinates in the same macro.");
                }
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
            return (false, ValidationMessages.CurrentPositionClickMustBeRelative);

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

    private static bool UsesCoordinateMode(EditorActionType actionType)
    {
        return actionType is
            EditorActionType.MouseMove or
            EditorActionType.MouseClick or
            EditorActionType.MouseDown or
            EditorActionType.MouseUp;
    }

    private static bool UsesGlobalCoordinateMode(EditorAction action)
    {
        if (!UsesCoordinateMode(action.Type))
        {
            return false;
        }

        // Current-position mouse button actions resolve from live cursor and should
        // not force other coordinate actions to switch absolute/relative mode.
        return !IsCurrentPositionMouseButtonAction(action);
    }

    private static bool IsCurrentPositionMouseButtonAction(EditorAction action)
    {
        return action.Type is EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp
            && action.UseCurrentPosition;
    }
    
    private static (bool IsValid, string? Error) ValidateTextInput(EditorAction action)
    {
        if (string.IsNullOrEmpty(action.Text))
            return (false, ValidationMessages.TextInputRequired);
        
        if (action.Text.Length > MaxTextInputLength)
            return (false, ValidationMessages.TextInputTooLong);
        
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
        if (!IsValidVariableName(action.ScriptVariableName))
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
            ScriptValueType.VariableReference => IsValidVariableName(action.ScriptValue)
                ? (true, null)
                : (false, "Referenced variable name is invalid."),
            _ => (false, ValidationMessages.ActionPayloadRequired)
        };
    }

    private static (bool IsValid, string? Error) ValidateIncDec(EditorAction action)
    {
        if (!IsValidVariableName(action.ScriptVariableName))
        {
            return (false, "Variable name is invalid. Allowed: letters, digits, underscore; cannot start with digit.");
        }

        if (!ValidateNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue))
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
        if (!ValidateNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue))
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
        if (!ValidateOperandToken(action.ScriptLeftOperandType, action.ScriptLeftOperand))
        {
            return (false, "Left operand is invalid for selected type.");
        }

        if (!ValidateOperandToken(action.ScriptRightOperandType, action.ScriptRightOperand))
        {
            return (false, "Right operand is invalid for selected type.");
        }

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateFor(EditorAction action)
    {
        if (!IsValidVariableName(action.ForVariableName))
        {
            return (false, "For-loop variable name is invalid.");
        }

        if (!ValidateNumericToken(action.ForStartType, action.ForStartValue))
        {
            if (action.ForStartType == ScriptNumericSourceType.VariableReference)
            {
                return (false, "For start variable reference must be a variable name (example: start or $start), not a number literal.");
            }

            return (false, "For start must be an integer or a valid variable reference.");
        }

        if (!ValidateNumericToken(action.ForEndType, action.ForEndValue))
        {
            if (action.ForEndType == ScriptNumericSourceType.VariableReference)
            {
                return (false, "For end variable reference must be a variable name (example: finish or $finish), not a number literal.");
            }

            return (false, "For end must be an integer or a valid variable reference.");
        }

        if (action.ForHasStep && !ValidateNumericToken(action.ForStepType, action.ForStepValue))
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

    private static bool ValidateNumericToken(ScriptNumericSourceType type, string token)
    {
        if (type == ScriptNumericSourceType.VariableReference)
        {
            return IsValidVariableName(token);
        }

        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static bool ValidateOperandToken(ScriptOperandType type, string token)
    {
        return type switch
        {
            ScriptOperandType.VariableReference => IsValidVariableName(token),
            ScriptOperandType.Number => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            ScriptOperandType.Boolean => bool.TryParse(token, out _),
            ScriptOperandType.Text => !string.IsNullOrWhiteSpace(token),
            _ => false
        };
    }

    private static bool IsValidVariableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var token = value.Trim();
        if (token.StartsWith('$'))
        {
            token = token[1..];
        }

        if (token.Length == 0)
        {
            return false;
        }

        if (!(token[0] == '_' || char.IsLetter(token[0])))
        {
            return false;
        }

        for (var i = 1; i < token.Length; i++)
        {
            if (!(token[i] == '_' || char.IsLetterOrDigit(token[i])))
            {
                return false;
            }
        }

        return true;
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
