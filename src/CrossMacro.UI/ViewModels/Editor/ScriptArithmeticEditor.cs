namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// Advanced arithmetic editing for block-argument numeric fields. Gated by action type: variable-math
/// amounts never expose the toggle; condition operands only for Number/Variable.
/// </summary>
public sealed class ScriptArithmeticEditor
{
    private readonly EditorViewModel _owner;
    private readonly ScriptArithmeticFieldEditor[] _fields;

    internal ScriptArithmeticEditor(EditorViewModel owner, VariableSuggestionScope suggestionScope)
    {
        _owner = owner;
        RepeatCount = new(this, new(
            static type => type is EditorActionType.RepeatBlockStart,
            static action => action.ScriptNumericValue,
            static (action, value) => action.ScriptNumericValue = value,
            static action => action.ScriptNumericSourceType,
            static (action, type) => action.ScriptNumericSourceType = type),
            () => _owner.Fields.ShowRepeatFields,
            suggestionScope);
        ForStart = new(this, new(
            static type => type is EditorActionType.ForBlockStart,
            static action => action.ForStartValue,
            static (action, value) => action.ForStartValue = value,
            static action => action.ForStartType,
            static (action, type) => action.ForStartType = type),
            () => _owner.Fields.ShowForFields,
            suggestionScope);
        ForEnd = new(this, new(
            static type => type is EditorActionType.ForBlockStart,
            static action => action.ForEndValue,
            static (action, value) => action.ForEndValue = value,
            static action => action.ForEndType,
            static (action, type) => action.ForEndType = type),
            () => _owner.Fields.ShowForFields,
            suggestionScope);
        ForStep = new(this, new(
            static type => type is EditorActionType.ForBlockStart,
            static action => action.ForStepValue,
            static (action, value) => action.ForStepValue = value,
            static action => action.ForStepType,
            static (action, type) => action.ForStepType = type),
            () => _owner.Fields.ShowForStepFields,
            suggestionScope);
        ConditionLeft = new(this, new(
            static type => type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart,
            static action => action.ScriptLeftOperand,
            static (action, value) => action.ScriptLeftOperand = value,
            static action => ToNumericSourceType(action.ScriptLeftOperandType),
            static (action, type) => action.ScriptLeftOperandType = ToOperandType(type),
            static action => IsNumericConditionOperandType(action.ScriptLeftOperandType)),
            () => _owner.Fields.ShowConditionFields,
            suggestionScope);
        ConditionRight = new(this, new(
            static type => type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart,
            static action => action.ScriptRightOperand,
            static (action, value) => action.ScriptRightOperand = value,
            static action => ToNumericSourceType(action.ScriptRightOperandType),
            static (action, type) => action.ScriptRightOperandType = ToOperandType(type),
            static action => IsNumericConditionOperandType(action.ScriptRightOperandType)),
            () => _owner.Fields.ShowConditionFields,
            suggestionScope);
        _fields = [RepeatCount, ForStart, ForEnd, ForStep, ConditionLeft, ConditionRight];
    }

    /// <summary>Operators offered by the Advanced arithmetic dropdown. Modulo is intentionally excluded.</summary>
    public static IReadOnlyList<ScriptArithmeticOperation> Operators { get; } =
    [
        ScriptArithmeticOperation.Add,
        ScriptArithmeticOperation.Subtract,
        ScriptArithmeticOperation.Multiply,
        ScriptArithmeticOperation.Divide,
    ];

    /// <summary>Condition operand types that can participate in arithmetic.</summary>
    public static IReadOnlyList<ScriptOperandType> NumericOperandTypes { get; } =
    [
        ScriptOperandType.Number,
        ScriptOperandType.VariableReference,
    ];

    public ScriptArithmeticFieldEditor RepeatCount { get; }

    public ScriptArithmeticFieldEditor ForStart { get; }

    public ScriptArithmeticFieldEditor ForEnd { get; }

    public ScriptArithmeticFieldEditor ForStep { get; }

    public ScriptArithmeticFieldEditor ConditionLeft { get; }

    public ScriptArithmeticFieldEditor ConditionRight { get; }

    internal EditorAction? SelectedAction => _owner.SelectedAction;

    internal bool HasAvailableVariableNames => _owner.Variables.HasAvailableNames;

    internal void NotifyChanged()
    {
        foreach (var field in _fields)
        {
            field.NotifyChanged();
        }
    }

    internal void ClearSuggestions()
    {
        foreach (var field in _fields)
        {
            field.ClearSuggestion();
        }
    }

    /// <summary>
    /// Re-derives every field's Advanced state from the given action. Runs on selection
    /// changes so panel state can never leak across actions or action types.
    /// </summary>
    internal void SyncFromModel(EditorAction? action)
    {
        foreach (var field in _fields)
        {
            if (action is null)
            {
                field.Reset();
            }
            else
            {
                field.SyncFromModel(action);
            }
        }
    }

    /// <summary>
    /// Normalization on action type / operand type changes: the shared
    /// <see cref="EditorAction.ScriptNumericValue"/> field must never keep an expression when
    /// the action becomes an inc/dec/mul/div amount, and a condition operand flipped to a
    /// non-numeric type drops back to its left operand instead of hiding a live expression.
    /// </summary>
    internal void NormalizeForAction(EditorAction action)
    {
        if (action.Type is EditorActionType.IncrementVariable or EditorActionType.DecrementVariable
            or EditorActionType.MultiplyVariable or EditorActionType.DivideVariable)
        {
            RepeatCount.CollapseIfUnsupported(action, writeType: true);
        }

        if (action.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart)
        {
            if (!IsNumericConditionOperandType(action.ScriptLeftOperandType))
            {
                ConditionLeft.CollapseIfUnsupported(action, writeType: false);
            }

            if (!IsNumericConditionOperandType(action.ScriptRightOperandType))
            {
                ConditionRight.CollapseIfUnsupported(action, writeType: false);
            }
        }
    }

    internal static string FormatExpressionOperandToken(ScriptNumericSourceType sourceType, string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length is 0
            ? string.Empty
            : EditorActionScriptTokens.FormatNumericToken(sourceType, trimmed, defaultValue: string.Empty);
    }

    internal static string ToSimpleStoredForm(ScriptNumericSourceType sourceType, string value)
    {
        var trimmed = value.Trim();
        return sourceType is ScriptNumericSourceType.VariableReference
            ? EditorActionScriptTokens.NormalizeVariableToken(trimmed)
            : trimmed;
    }

    internal static bool IsNumericConditionOperandType(ScriptOperandType? operandType)
    {
        return operandType is ScriptOperandType.Number or ScriptOperandType.VariableReference;
    }

    internal static ScriptNumericSourceType ToNumericSourceType(ScriptOperandType operandType)
    {
        return operandType is ScriptOperandType.VariableReference
            ? ScriptNumericSourceType.VariableReference
            : ScriptNumericSourceType.Number;
    }

    internal static ScriptOperandType ToOperandType(ScriptNumericSourceType sourceType)
    {
        return sourceType is ScriptNumericSourceType.VariableReference
            ? ScriptOperandType.VariableReference
            : ScriptOperandType.Number;
    }
}
