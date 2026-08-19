
namespace CrossMacro.UI.ViewModels;

public partial class EditorViewModel
{
    private ScriptConditionOperator[] GetConditionOperatorsForSelectedAction()
    {
        if (SelectedAction is not null && AreNumericComparisonOperatorsAllowed(SelectedAction))
        {
            return Enum.GetValues<ScriptConditionOperator>();
        }

        return
        [
            ScriptConditionOperator.Equals,
            ScriptConditionOperator.NotEquals,
        ];
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
            ScriptOperandType.Color => ScriptVariableKind.Color,
            ScriptOperandType.VariableReference => InferVariableKind(operand, selectedAction),
            _ => ScriptVariableKind.Unknown,
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
            if (kind is not ScriptVariableKind.Unknown)
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
                EditorActionScreenReadingVariableRole.None => ScriptVariableKind.Unknown,
                _ => throw new ArgumentOutOfRangeException(nameof(action), screenReadingPayload.GetOutputVariableRole(variableName), message: null),
            };
        }

        return action.Type switch
        {
            EditorActionType.SetVariable when string.Equals(action.ScriptVariableName, variableName, StringComparison.Ordinal) => action.ScriptValueType switch
            {
                ScriptValueType.Number => ScriptVariableKind.Number,
                ScriptValueType.Text => ScriptVariableKind.Text,
                ScriptValueType.Boolean => ScriptVariableKind.Boolean,
                ScriptValueType.VariableReference => ScriptVariableKind.Unknown,
                _ => throw new ArgumentOutOfRangeException(nameof(action), action.ScriptValueType, message: null),
            },
            EditorActionType.ForBlockStart when string.Equals(action.ForVariableName, variableName, StringComparison.Ordinal) => ScriptVariableKind.Number,
            EditorActionType.MousePosition when string.Equals(action.MousePositionXVariableName, variableName, StringComparison.Ordinal) => ScriptVariableKind.Number,
            EditorActionType.MousePosition when string.Equals(action.MousePositionYVariableName, variableName, StringComparison.Ordinal) => ScriptVariableKind.Number,
            EditorActionType.IncrementVariable or EditorActionType.DecrementVariable or EditorActionType.MultiplyVariable or EditorActionType.DivideVariable when string.Equals(action.ScriptVariableName, variableName, StringComparison.Ordinal) => ScriptVariableKind.Number,
            EditorActionType.SetVariable => ScriptVariableKind.Unknown,
            EditorActionType.ForBlockStart => ScriptVariableKind.Unknown,
            EditorActionType.MousePosition => ScriptVariableKind.Unknown,
            EditorActionType.IncrementVariable or EditorActionType.DecrementVariable => ScriptVariableKind.Unknown,
            EditorActionType.MultiplyVariable or EditorActionType.DivideVariable => ScriptVariableKind.Unknown,
            EditorActionType.MouseMove => ScriptVariableKind.Unknown,
            EditorActionType.MouseClick => ScriptVariableKind.Unknown,
            EditorActionType.MouseDown => ScriptVariableKind.Unknown,
            EditorActionType.MouseUp => ScriptVariableKind.Unknown,
            EditorActionType.KeyPress => ScriptVariableKind.Unknown,
            EditorActionType.KeyDown => ScriptVariableKind.Unknown,
            EditorActionType.KeyUp => ScriptVariableKind.Unknown,
            EditorActionType.Delay => ScriptVariableKind.Unknown,
            EditorActionType.ScrollVertical => ScriptVariableKind.Unknown,
            EditorActionType.ScrollHorizontal => ScriptVariableKind.Unknown,
            EditorActionType.TextInput => ScriptVariableKind.Unknown,
            EditorActionType.RepeatBlockStart => ScriptVariableKind.Unknown,
            EditorActionType.IfBlockStart => ScriptVariableKind.Unknown,
            EditorActionType.ElseBlockStart => ScriptVariableKind.Unknown,
            EditorActionType.WhileBlockStart => ScriptVariableKind.Unknown,
            EditorActionType.BlockEnd => ScriptVariableKind.Unknown,
            EditorActionType.Break => ScriptVariableKind.Unknown,
            EditorActionType.Continue => ScriptVariableKind.Unknown,
            EditorActionType.PixelColor => ScriptVariableKind.Unknown,
            EditorActionType.WaitColor => ScriptVariableKind.Unknown,
            EditorActionType.PixelSearch => ScriptVariableKind.Unknown,
            EditorActionType.ImageSearch => ScriptVariableKind.Unknown,
            EditorActionType.ImageClick => ScriptVariableKind.Unknown,
            EditorActionType.WaitImage => ScriptVariableKind.Unknown,
            EditorActionType.ClipboardGet => ScriptVariableKind.Unknown,
            EditorActionType.ClipboardSet => ScriptVariableKind.Unknown,
            EditorActionType.ShellCommand => ScriptVariableKind.Unknown,
            EditorActionType.Screenshot => ScriptVariableKind.Unknown,
            EditorActionType.WindowCommand => ScriptVariableKind.Unknown,
            EditorActionType.RawScriptStep => ScriptVariableKind.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action.Type, message: null),
        };
    }
}
