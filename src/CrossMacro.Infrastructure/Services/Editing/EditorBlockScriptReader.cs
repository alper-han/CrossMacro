using System.Diagnostics.CodeAnalysis;

namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>Projects block headers and flow-control script steps into editable actions.</summary>
internal static class EditorBlockScriptReader
{
    internal static bool TryReadStep(string step, [NotNullWhen(true)] out EditorAction? action)
    {
        if (TryParseRepeatStep(step, out action)
            || TryParseConditionStep(step, "if", EditorActionType.IfBlockStart, out action)
            || TryParseConditionStep(step, "while", EditorActionType.WhileBlockStart, out action)
            || TryParseForStep(step, out action))
        {
            return true;
        }

        var markerType = ReadMarkerType(step);
        action = markerType is { } type ? new EditorAction { Type = type } : null;
        return action is not null;
    }

    internal static bool TryParseRepeatStep(string step, [NotNullWhen(true)] out EditorAction? action)
    {
        action = null;
        if (!RunScriptHeaderParser.TryReadConditionHeader(step, "repeat", out var token) || token.Length is 0)
        {
            return false;
        }

        action = new EditorAction { Type = EditorActionType.RepeatBlockStart };
        if (EditorScriptTokenReader.TryParseBinaryExpression(token, out var sourceType, out var tokenValue)
            || EditorActionScriptTokens.TryParseNumericToken(token, out sourceType, out tokenValue))
        {
            action.ScriptNumericSourceType = sourceType;
            action.ScriptNumericValue = tokenValue;
        }
        else
        {
            action.Text = token;
        }

        return true;
    }

    internal static bool TryParseConditionStep(string step, string keyword, EditorActionType actionType, [NotNullWhen(true)] out EditorAction? action)
    {
        action = null;
        if (!RunScriptHeaderParser.TryReadConditionHeader(step, keyword, out var condition) || condition.Length is 0)
        {
            return false;
        }

        if (!RunScriptConditionParser.TryParse(condition, out var parsedCondition, out _)
            || parsedCondition is null
            || !ScriptConditionOperatorSyntax.TryParse(parsedCondition.OperatorToken, out var conditionOperator))
        {
            action = new EditorAction { Type = actionType, Text = condition };
            return true;
        }

        var preferColor = conditionOperator is ScriptConditionOperator.Equals or ScriptConditionOperator.NotEquals;
        var allowArithmetic = conditionOperator is ScriptConditionOperator.GreaterThan or ScriptConditionOperator.GreaterThanOrEqual
            or ScriptConditionOperator.LessThan or ScriptConditionOperator.LessThanOrEqual;
        if (!EditorScriptTokenReader.TryParseConditionOperandToken(parsedCondition.LeftToken, allowArithmetic, preferColor, out var leftType, out var leftValue)
            || !EditorScriptTokenReader.TryParseConditionOperandToken(parsedCondition.RightToken, allowArithmetic, preferColor, out var rightType, out var rightValue))
        {
            return false;
        }

        action = new EditorAction
        {
            Type = actionType,
            ScriptLeftOperandType = leftType,
            ScriptLeftOperand = leftValue,
            ScriptConditionOperator = conditionOperator,
            ScriptRightOperandType = rightType,
            ScriptRightOperand = rightValue,
        };
        return true;
    }

    /// <summary>
    /// Restores a <c>for</c> header. Source the structured fields cannot express stays as legacy text for an
    /// explicit editor repair.
    /// </summary>
    internal static bool TryParseForStep(string step, [NotNullWhen(true)] out EditorAction? action)
    {
        action = null;
        if (!RunScriptHeaderParser.TryReadConditionHeader(step, "for", out var body) || body.Length is 0)
        {
            return false;
        }

        action = new EditorAction { Type = EditorActionType.ForBlockStart, Text = body };
        if (!RunScriptHeaderParser.TryParseForHeader(step, out var header, out var error) || error is not null || header is null)
        {
            return true;
        }

        if (!TryParseNumericSegment(header.StartToken, out var startType, out var startValue)
            || !TryParseNumericSegment(header.EndToken, out var endType, out var endValue)
            || !TryParseNumericSegment(header.StepToken ?? "1", out var stepType, out var stepValue))
        {
            return true;
        }

        action = new EditorAction
        {
            Type = EditorActionType.ForBlockStart,
            ForVariableName = header.VariableName,
            ForStartType = startType,
            ForStartValue = startValue,
            ForEndType = endType,
            ForEndValue = endValue,
            ForHasStep = header.HasExplicitStep,
            ForStepType = stepType,
            ForStepValue = stepValue,
        };
        return true;
    }

    private static EditorActionType? ReadMarkerType(string step)
    {
        if (RunScriptSyntax.IsElseHeader(step))
        {
            return EditorActionType.ElseBlockStart;
        }

        if (RunScriptSyntax.IsBreakCommand(step))
        {
            return EditorActionType.Break;
        }

        if (RunScriptSyntax.IsContinueCommand(step))
        {
            return EditorActionType.Continue;
        }

        return RunScriptSyntax.IsBlockEndToken(step) ? EditorActionType.BlockEnd : null;
    }

    private static bool TryParseNumericSegment(string segment, out ScriptNumericSourceType sourceType, out string value) =>
        EditorScriptTokenReader.TryParseNumericSegment(
            segment.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            out sourceType,
            out value);
}
