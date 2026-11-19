namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>Validates variable, arithmetic, block-header, clipboard and mouse-position actions.</summary>
internal static class EditorScriptActionValidator
{
    private const string InvalidVariableName = "Variable name is invalid. Allowed: letters, digits, underscore; cannot start with digit.";

    internal static (bool IsValid, string? Error) ValidateActionPayload(EditorAction action)
    {
        if (action.PreferLegacyScriptText && !string.IsNullOrWhiteSpace(action.Text))
        {
            return (true, null);
        }

        if (action.Type is EditorActionType.SetVariable)
        {
            return ValidateSetVariable(action);
        }

        if (action.Type is EditorActionType.IncrementVariable or EditorActionType.DecrementVariable or EditorActionType.MultiplyVariable or EditorActionType.DivideVariable)
        {
            return ValidateIncDec(action);
        }

        if (action.Type is EditorActionType.RepeatBlockStart)
        {
            return ValidateRepeat(action);
        }

        if (action.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart)
        {
            return ValidateCondition(action);
        }

        return action.Type is EditorActionType.ForBlockStart
            ? ValidateFor(action)
            : (false, ValidationMessages.ActionPayloadRequired);
    }

    internal static (bool IsValid, string? Error) ValidateClipboardGet(EditorAction action)
    {
        return ScriptIdentifierSyntax.IsValidVariableName(action.ScriptVariableName)
            ? (true, null)
            : (false, "Clipboard destination variable name is invalid. Allowed: letters, digits, underscore; cannot start with digit.");
    }

    internal static (bool IsValid, string? Error) ValidateClipboardSet(EditorAction action)
    {
        return string.IsNullOrEmpty(action.Text)
            ? (false, "Clipboard text cannot be empty.")
            : (true, null);
    }

    internal static (bool IsValid, string? Error) ValidateCopySelectionToVariable(EditorAction action)
    {
        if (!Enum.IsDefined(action.ClipboardCopyShortcut))
        {
            return (false, "Copy shortcut is invalid.");
        }

        return ValidateClipboardGet(action);
    }

    internal static (bool IsValid, string? Error) ValidateMousePosition(EditorAction action)
    {
        if (!ScriptIdentifierSyntax.IsValidVariableName(action.MousePositionXVariableName)
            || !ScriptIdentifierSyntax.IsValidVariableName(action.MousePositionYVariableName))
        {
            return (false, "Mouse position destination variable names are invalid. Allowed: letters, digits, underscore; cannot start with digit.");
        }

        return string.Equals(action.MousePositionXVariableName, action.MousePositionYVariableName, StringComparison.OrdinalIgnoreCase)
            ? (false, "Mouse position X and Y destination variables must be different.")
            : (true, null);
    }

    private static (bool IsValid, string? Error) ValidateSetVariable(EditorAction action)
    {
        if (!ScriptIdentifierSyntax.IsValidVariableName(action.ScriptVariableName))
        {
            return (false, InvalidVariableName);
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
            ScriptValueType.VariableReference => ScriptIdentifierSyntax.IsValidVariableName(action.ScriptValue)
                ? (true, null)
                : (false, "Referenced variable name is invalid."),
            _ => (false, ValidationMessages.ActionPayloadRequired),
        };
    }

    private static (bool IsValid, string? Error) ValidateIncDec(EditorAction action)
    {
        if (!ScriptIdentifierSyntax.IsValidVariableName(action.ScriptVariableName))
        {
            return (false, InvalidVariableName);
        }

        if (EditorActionScriptTokens.ValidateNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue))
        {
            return (true, null);
        }

        return action.ScriptNumericSourceType is ScriptNumericSourceType.VariableReference
            ? (false, "Amount variable reference must be a variable name (example: step or $step), not a number literal.")
            : (false, "Amount must be an integer or a valid variable reference.");
    }

    private static (bool IsValid, string? Error) ValidateRepeat(EditorAction action)
    {
        var count = ValidateBlockNumber(
            action.ScriptNumericSourceType,
            action.ScriptNumericValue,
            "Repeat variable reference must be a variable name (example: count or $count), not a number literal.",
            "Repeat count must be an integer or a valid variable reference.");
        if (!count.IsValid)
        {
            return count;
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
        if (!ScriptIdentifierSyntax.IsValidVariableName(action.ForVariableName))
        {
            return (false, "For-loop variable name is invalid.");
        }

        var start = ValidateBlockNumber(
            action.ForStartType,
            action.ForStartValue,
            "For start variable reference must be a variable name (example: start or $start), not a number literal.",
            "For start must be an integer or a valid variable reference.");
        if (!start.IsValid)
        {
            return start;
        }

        var end = ValidateBlockNumber(
            action.ForEndType,
            action.ForEndValue,
            "For end variable reference must be a variable name (example: finish or $finish), not a number literal.",
            "For end must be an integer or a valid variable reference.");
        if (!end.IsValid)
        {
            return end;
        }

        if (!action.ForHasStep)
        {
            return (true, null);
        }

        var step = ValidateBlockNumber(
            action.ForStepType,
            action.ForStepValue,
            "For step variable reference must be a variable name (example: step or $step), not a number literal.",
            "For step must be an integer or a valid variable reference.");
        if (!step.IsValid)
        {
            return step;
        }

        return action.ForStepType is ScriptNumericSourceType.Number
            && int.TryParse(action.ForStepValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericStep)
            && numericStep is 0
                ? (false, "For step cannot be 0.")
                : (true, null);
    }

    private static (bool IsValid, string? Error) ValidateBlockNumber(
        ScriptNumericSourceType sourceType,
        string value,
        string variableReferenceMessage,
        string invalidMessage)
    {
        if (EditorActionScriptTokens.ValidateBlockNumericToken(sourceType, value))
        {
            return (true, null);
        }

        return (false, sourceType is ScriptNumericSourceType.VariableReference ? variableReferenceMessage : invalidMessage);
    }
}
