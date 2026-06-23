using System;
using System.Collections.Generic;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.ViewModels;

public partial class EditorViewModel
{
    private IEnumerable<ScriptConditionOperator> GetConditionOperatorsForSelectedAction()
    {
        if (SelectedAction != null
            && AreNumericComparisonOperatorsAllowed(SelectedAction))
        {
            return Enum.GetValues<ScriptConditionOperator>();
        }

        return new[]
        {
            ScriptConditionOperator.Equals,
            ScriptConditionOperator.NotEquals
        };
    }

    private bool IsOperatorValidForOperands(EditorAction action)
    {
        if (AreNumericComparisonOperatorsAllowed(action))
        {
            return true;
        }

        return action.ScriptConditionOperator is ScriptConditionOperator.Equals or ScriptConditionOperator.NotEquals;
    }

    private bool AreNumericComparisonOperatorsAllowed(EditorAction action)
    {
        var leftKind = ResolveConditionOperandKind(action.ScriptLeftOperandType, action.ScriptLeftOperand, action);
        var rightKind = ResolveConditionOperandKind(action.ScriptRightOperandType, action.ScriptRightOperand, action);
        return IsNumericComparableKind(leftKind) && IsNumericComparableKind(rightKind);
    }

    private static bool IsNumericComparableKind(ScriptVariableKind kind)
    {
        return kind is ScriptVariableKind.Number or ScriptVariableKind.Unknown;
    }

    private ScriptVariableKind ResolveConditionOperandKind(
        ScriptOperandType operandType,
        string operand,
        EditorAction selectedAction)
    {
        return operandType switch
        {
            ScriptOperandType.Number => ScriptVariableKind.Number,
            ScriptOperandType.Text => ScriptVariableKind.Text,
            ScriptOperandType.Boolean => ScriptVariableKind.Boolean,
            ScriptOperandType.VariableReference => InferVariableKind(operand, selectedAction),
            _ => ScriptVariableKind.Unknown
        };
    }

    private ScriptVariableKind InferVariableKind(string variableName, EditorAction selectedAction)
    {
        if (!EditorActionScriptTokens.IsValidVariableName(variableName))
        {
            return ScriptVariableKind.Unknown;
        }

        foreach (var action in ActionsForInference(selectedAction))
        {
            var kind = InferVariableKindFromAction(variableName, action);
            if (kind != ScriptVariableKind.Unknown)
            {
                return kind;
            }
        }

        return ScriptVariableKind.Unknown;
    }

    private IEnumerable<EditorAction> ActionsForInference(EditorAction selectedAction)
    {
        var selectedIndex = Actions.IndexOf(selectedAction);
        var lastDefinitionIndex = selectedIndex >= 0 ? selectedIndex - 1 : Actions.Count - 1;

        for (var index = lastDefinitionIndex; index >= 0; index--)
        {
            yield return Actions[index];
        }

        if (!Actions.Contains(selectedAction))
        {
            yield return selectedAction;
        }
    }

    private static ScriptVariableKind InferVariableKindFromAction(string variableName, EditorAction action)
    {
        if (action.TryGetScreenReadingPayload(out var screenReadingPayload))
        {
            return screenReadingPayload.GetOutputVariableRole(variableName) switch
            {
                EditorActionScreenReadingVariableRole.Color => ScriptVariableKind.Color,
                EditorActionScreenReadingVariableRole.Boolean => ScriptVariableKind.Boolean,
                EditorActionScreenReadingVariableRole.Number => ScriptVariableKind.Number,
                _ => ScriptVariableKind.Unknown
            };
        }

        return action.Type switch
        {
            EditorActionType.SetVariable when string.Equals(action.ScriptVariableName, variableName, StringComparison.Ordinal) => action.ScriptValueType switch
            {
                ScriptValueType.Number => ScriptVariableKind.Number,
                ScriptValueType.Text => ScriptVariableKind.Text,
                ScriptValueType.Boolean => ScriptVariableKind.Boolean,
                _ => ScriptVariableKind.Unknown
            },
            EditorActionType.ForBlockStart when string.Equals(action.ForVariableName, variableName, StringComparison.Ordinal) => ScriptVariableKind.Number,
            EditorActionType.IncrementVariable or EditorActionType.DecrementVariable when string.Equals(action.ScriptVariableName, variableName, StringComparison.Ordinal) => ScriptVariableKind.Number,
            _ => ScriptVariableKind.Unknown
        };
    }
}
