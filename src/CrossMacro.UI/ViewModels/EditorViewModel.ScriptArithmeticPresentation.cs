
namespace CrossMacro.UI.ViewModels;

/// <summary>
/// Advanced arithmetic editing for block-argument numeric fields. The canonical expression
/// string lives in the existing model value property; this state only decomposes/recomposes
/// it via <see cref="ScriptNumericExpression"/>. Gated by action type: variable-math amounts
/// never expose the toggle; condition operands only for Number/Variable.
/// </summary>
public partial class EditorViewModel
{
    /// <summary>Operators offered by the Advanced arithmetic dropdown. Modulo is intentionally excluded.</summary>
    public static IReadOnlyList<ScriptArithmeticOperation> ScriptArithmeticOperators { get; } =
    [
        ScriptArithmeticOperation.Add,
        ScriptArithmeticOperation.Subtract,
        ScriptArithmeticOperation.Multiply,
        ScriptArithmeticOperation.Divide,
    ];

    /// <summary>Condition operand types that can participate in arithmetic.</summary>
    public static IReadOnlyList<ScriptOperandType> ScriptNumericOperandTypes { get; } =
    [
        ScriptOperandType.Number,
        ScriptOperandType.VariableReference,
    ];

    private readonly ScriptArithmeticFieldState _repeatCountExpr = new();
    private readonly ScriptArithmeticFieldState _forStartExpr = new();
    private readonly ScriptArithmeticFieldState _forEndExpr = new();
    private readonly ScriptArithmeticFieldState _forStepExpr = new();
    private readonly ScriptArithmeticFieldState _conditionLeftExpr = new();
    private readonly ScriptArithmeticFieldState _conditionRightExpr = new();

    private string? _selectedRepeatCountExprRightVariableSuggestion;
    private string? _selectedForStartExprRightVariableSuggestion;
    private string? _selectedForEndExprRightVariableSuggestion;
    private string? _selectedForStepExprRightVariableSuggestion;
    private string? _selectedConditionLeftExprRightVariableSuggestion;
    private string? _selectedConditionRightExprRightVariableSuggestion;

    private static readonly ScriptArithmeticFieldAccess RepeatCountAccess = new(
        static type => type is EditorActionType.RepeatBlockStart,
        static action => action.ScriptNumericValue,
        static (action, value) => action.ScriptNumericValue = value,
        static action => action.ScriptNumericSourceType,
        static (action, type) => action.ScriptNumericSourceType = type);

    private static readonly ScriptArithmeticFieldAccess ForStartAccess = new(
        static type => type is EditorActionType.ForBlockStart,
        static action => action.ForStartValue,
        static (action, value) => action.ForStartValue = value,
        static action => action.ForStartType,
        static (action, type) => action.ForStartType = type);

    private static readonly ScriptArithmeticFieldAccess ForEndAccess = new(
        static type => type is EditorActionType.ForBlockStart,
        static action => action.ForEndValue,
        static (action, value) => action.ForEndValue = value,
        static action => action.ForEndType,
        static (action, type) => action.ForEndType = type);

    private static readonly ScriptArithmeticFieldAccess ForStepAccess = new(
        static type => type is EditorActionType.ForBlockStart,
        static action => action.ForStepValue,
        static (action, value) => action.ForStepValue = value,
        static action => action.ForStepType,
        static (action, type) => action.ForStepType = type);

    private static readonly ScriptArithmeticFieldAccess ConditionLeftAccess = new(
        static type => type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart,
        static action => action.ScriptLeftOperand,
        static (action, value) => action.ScriptLeftOperand = value,
        static action => ToNumericSourceType(action.ScriptLeftOperandType),
        static (action, type) => action.ScriptLeftOperandType = ToOperandType(type),
        static action => IsNumericConditionOperandType(action.ScriptLeftOperandType));

    private static readonly ScriptArithmeticFieldAccess ConditionRightAccess = new(
        static type => type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart,
        static action => action.ScriptRightOperand,
        static (action, value) => action.ScriptRightOperand = value,
        static action => ToNumericSourceType(action.ScriptRightOperandType),
        static (action, type) => action.ScriptRightOperandType = ToOperandType(type),
        static action => IsNumericConditionOperandType(action.ScriptRightOperandType));

    #region Repeat count

    public bool IsRepeatCountAdvanced
    {
        get => _repeatCountExpr.IsAdvanced;
        set => SetFieldAdvanced(_repeatCountExpr, RepeatCountAccess, value);
    }

    public string RepeatCountExprLeftValue
    {
        get => _repeatCountExpr.LeftValue;
        set => SetFieldLeftValue(_repeatCountExpr, RepeatCountAccess, value);
    }

    public ScriptNumericSourceType RepeatCountExprLeftType
    {
        get => _repeatCountExpr.LeftType;
        set => SetFieldLeftType(_repeatCountExpr, RepeatCountAccess, value);
    }

    public ScriptArithmeticOperation RepeatCountExprOperator
    {
        get => _repeatCountExpr.Operator;
        set => SetFieldOperator(_repeatCountExpr, RepeatCountAccess, value);
    }

    public string RepeatCountExprRightValue
    {
        get => _repeatCountExpr.RightValue;
        set => SetFieldRightValue(_repeatCountExpr, RepeatCountAccess, value);
    }

    public ScriptNumericSourceType RepeatCountExprRightType
    {
        get => _repeatCountExpr.RightType;
        set => SetFieldRightType(_repeatCountExpr, RepeatCountAccess, value);
    }

    public string? SelectedRepeatCountExprRightVariableSuggestion
    {
        get => _selectedRepeatCountExprRightVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedRepeatCountExprRightVariableSuggestion, value, nameof(SelectedRepeatCountExprRightVariableSuggestion), suggestion =>
        {
            if (ShowRepeatCountAdvancedPanel && _repeatCountExpr.RightType is ScriptNumericSourceType.VariableReference)
            {
                RepeatCountExprRightValue = suggestion;
            }
        });
    }

    public bool ShowRepeatCountAdvancedToggle => ShowRepeatFields;
    public bool ShowRepeatCountAdvancedPanel => ShowRepeatFields && _repeatCountExpr.IsAdvanced;
    public bool ShowRepeatCountSimpleFields => ShowRepeatFields && !_repeatCountExpr.IsAdvanced;
    public bool ShowRepeatCountExprRightVariablePicker =>
        ShowRepeatCountAdvancedPanel
        && _repeatCountExpr.RightType is ScriptNumericSourceType.VariableReference
        && HasAvailableVariableNames;

    #endregion

    #region For start / end / step

    public bool IsForStartAdvanced
    {
        get => _forStartExpr.IsAdvanced;
        set => SetFieldAdvanced(_forStartExpr, ForStartAccess, value);
    }

    public string ForStartExprLeftValue
    {
        get => _forStartExpr.LeftValue;
        set => SetFieldLeftValue(_forStartExpr, ForStartAccess, value);
    }

    public ScriptNumericSourceType ForStartExprLeftType
    {
        get => _forStartExpr.LeftType;
        set => SetFieldLeftType(_forStartExpr, ForStartAccess, value);
    }

    public ScriptArithmeticOperation ForStartExprOperator
    {
        get => _forStartExpr.Operator;
        set => SetFieldOperator(_forStartExpr, ForStartAccess, value);
    }

    public string ForStartExprRightValue
    {
        get => _forStartExpr.RightValue;
        set => SetFieldRightValue(_forStartExpr, ForStartAccess, value);
    }

    public ScriptNumericSourceType ForStartExprRightType
    {
        get => _forStartExpr.RightType;
        set => SetFieldRightType(_forStartExpr, ForStartAccess, value);
    }

    public string? SelectedForStartExprRightVariableSuggestion
    {
        get => _selectedForStartExprRightVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedForStartExprRightVariableSuggestion, value, nameof(SelectedForStartExprRightVariableSuggestion), suggestion =>
        {
            if (ShowForStartAdvancedPanel && _forStartExpr.RightType is ScriptNumericSourceType.VariableReference)
            {
                ForStartExprRightValue = suggestion;
            }
        });
    }

    public bool ShowForStartAdvancedToggle => ShowForFields;
    public bool ShowForStartAdvancedPanel => ShowForFields && _forStartExpr.IsAdvanced;
    public bool ShowForStartSimpleFields => ShowForFields && !_forStartExpr.IsAdvanced;
    public bool ShowForStartExprRightVariablePicker =>
        ShowForStartAdvancedPanel
        && _forStartExpr.RightType is ScriptNumericSourceType.VariableReference
        && HasAvailableVariableNames;

    public bool IsForEndAdvanced
    {
        get => _forEndExpr.IsAdvanced;
        set => SetFieldAdvanced(_forEndExpr, ForEndAccess, value);
    }

    public string ForEndExprLeftValue
    {
        get => _forEndExpr.LeftValue;
        set => SetFieldLeftValue(_forEndExpr, ForEndAccess, value);
    }

    public ScriptNumericSourceType ForEndExprLeftType
    {
        get => _forEndExpr.LeftType;
        set => SetFieldLeftType(_forEndExpr, ForEndAccess, value);
    }

    public ScriptArithmeticOperation ForEndExprOperator
    {
        get => _forEndExpr.Operator;
        set => SetFieldOperator(_forEndExpr, ForEndAccess, value);
    }

    public string ForEndExprRightValue
    {
        get => _forEndExpr.RightValue;
        set => SetFieldRightValue(_forEndExpr, ForEndAccess, value);
    }

    public ScriptNumericSourceType ForEndExprRightType
    {
        get => _forEndExpr.RightType;
        set => SetFieldRightType(_forEndExpr, ForEndAccess, value);
    }

    public string? SelectedForEndExprRightVariableSuggestion
    {
        get => _selectedForEndExprRightVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedForEndExprRightVariableSuggestion, value, nameof(SelectedForEndExprRightVariableSuggestion), suggestion =>
        {
            if (ShowForEndAdvancedPanel && _forEndExpr.RightType is ScriptNumericSourceType.VariableReference)
            {
                ForEndExprRightValue = suggestion;
            }
        });
    }

    public bool ShowForEndAdvancedToggle => ShowForFields;
    public bool ShowForEndAdvancedPanel => ShowForFields && _forEndExpr.IsAdvanced;
    public bool ShowForEndSimpleFields => ShowForFields && !_forEndExpr.IsAdvanced;
    public bool ShowForEndExprRightVariablePicker =>
        ShowForEndAdvancedPanel
        && _forEndExpr.RightType is ScriptNumericSourceType.VariableReference
        && HasAvailableVariableNames;

    public bool IsForStepAdvanced
    {
        get => _forStepExpr.IsAdvanced;
        set => SetFieldAdvanced(_forStepExpr, ForStepAccess, value);
    }

    public string ForStepExprLeftValue
    {
        get => _forStepExpr.LeftValue;
        set => SetFieldLeftValue(_forStepExpr, ForStepAccess, value);
    }

    public ScriptNumericSourceType ForStepExprLeftType
    {
        get => _forStepExpr.LeftType;
        set => SetFieldLeftType(_forStepExpr, ForStepAccess, value);
    }

    public ScriptArithmeticOperation ForStepExprOperator
    {
        get => _forStepExpr.Operator;
        set => SetFieldOperator(_forStepExpr, ForStepAccess, value);
    }

    public string ForStepExprRightValue
    {
        get => _forStepExpr.RightValue;
        set => SetFieldRightValue(_forStepExpr, ForStepAccess, value);
    }

    public ScriptNumericSourceType ForStepExprRightType
    {
        get => _forStepExpr.RightType;
        set => SetFieldRightType(_forStepExpr, ForStepAccess, value);
    }

    public string? SelectedForStepExprRightVariableSuggestion
    {
        get => _selectedForStepExprRightVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedForStepExprRightVariableSuggestion, value, nameof(SelectedForStepExprRightVariableSuggestion), suggestion =>
        {
            if (ShowForStepAdvancedPanel && _forStepExpr.RightType is ScriptNumericSourceType.VariableReference)
            {
                ForStepExprRightValue = suggestion;
            }
        });
    }

    public bool ShowForStepAdvancedToggle => ShowForStepFields;
    public bool ShowForStepAdvancedPanel => ShowForStepFields && _forStepExpr.IsAdvanced;
    public bool ShowForStepSimpleFields => ShowForStepFields && !_forStepExpr.IsAdvanced;
    public bool ShowForStepExprRightVariablePicker =>
        ShowForStepAdvancedPanel
        && _forStepExpr.RightType is ScriptNumericSourceType.VariableReference
        && HasAvailableVariableNames;

    #endregion

    #region Condition operands

    public bool IsConditionLeftAdvanced
    {
        get => _conditionLeftExpr.IsAdvanced;
        set => SetFieldAdvanced(_conditionLeftExpr, ConditionLeftAccess, value);
    }

    public ScriptOperandType ConditionLeftExprLeftType
    {
        get => ToOperandType(_conditionLeftExpr.LeftType);
        set => SetFieldLeftType(_conditionLeftExpr, ConditionLeftAccess, ToNumericSourceType(value));
    }

    public string ConditionLeftExprLeftValue
    {
        get => _conditionLeftExpr.LeftValue;
        set => SetFieldLeftValue(_conditionLeftExpr, ConditionLeftAccess, value);
    }

    public ScriptArithmeticOperation ConditionLeftExprOperator
    {
        get => _conditionLeftExpr.Operator;
        set => SetFieldOperator(_conditionLeftExpr, ConditionLeftAccess, value);
    }

    public ScriptOperandType ConditionLeftExprRightType
    {
        get => ToOperandType(_conditionLeftExpr.RightType);
        set => SetFieldRightType(_conditionLeftExpr, ConditionLeftAccess, ToNumericSourceType(value));
    }

    public string ConditionLeftExprRightValue
    {
        get => _conditionLeftExpr.RightValue;
        set => SetFieldRightValue(_conditionLeftExpr, ConditionLeftAccess, value);
    }

    public string? SelectedConditionLeftExprRightVariableSuggestion
    {
        get => _selectedConditionLeftExprRightVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedConditionLeftExprRightVariableSuggestion, value, nameof(SelectedConditionLeftExprRightVariableSuggestion), suggestion =>
        {
            if (ShowConditionLeftAdvancedPanel && _conditionLeftExpr.RightType is ScriptNumericSourceType.VariableReference)
            {
                ConditionLeftExprRightValue = suggestion;
            }
        });
    }

    public bool ShowConditionLeftAdvancedToggle =>
        ShowConditionFields && IsNumericConditionOperandType(SelectedAction?.ScriptLeftOperandType);
    public bool ShowConditionLeftAdvancedPanel => ShowConditionLeftAdvancedToggle && _conditionLeftExpr.IsAdvanced;
    public bool ShowConditionLeftSimpleFields => ShowConditionFields && !ShowConditionLeftAdvancedPanel;
    public bool ShowConditionLeftExprRightVariablePicker =>
        ShowConditionLeftAdvancedPanel
        && _conditionLeftExpr.RightType is ScriptNumericSourceType.VariableReference
        && HasAvailableVariableNames;

    public bool IsConditionRightAdvanced
    {
        get => _conditionRightExpr.IsAdvanced;
        set => SetFieldAdvanced(_conditionRightExpr, ConditionRightAccess, value);
    }

    public ScriptOperandType ConditionRightExprLeftType
    {
        get => ToOperandType(_conditionRightExpr.LeftType);
        set => SetFieldLeftType(_conditionRightExpr, ConditionRightAccess, ToNumericSourceType(value));
    }

    public string ConditionRightExprLeftValue
    {
        get => _conditionRightExpr.LeftValue;
        set => SetFieldLeftValue(_conditionRightExpr, ConditionRightAccess, value);
    }

    public ScriptArithmeticOperation ConditionRightExprOperator
    {
        get => _conditionRightExpr.Operator;
        set => SetFieldOperator(_conditionRightExpr, ConditionRightAccess, value);
    }

    public ScriptOperandType ConditionRightExprRightType
    {
        get => ToOperandType(_conditionRightExpr.RightType);
        set => SetFieldRightType(_conditionRightExpr, ConditionRightAccess, ToNumericSourceType(value));
    }

    public string ConditionRightExprRightValue
    {
        get => _conditionRightExpr.RightValue;
        set => SetFieldRightValue(_conditionRightExpr, ConditionRightAccess, value);
    }

    public string? SelectedConditionRightExprRightVariableSuggestion
    {
        get => _selectedConditionRightExprRightVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedConditionRightExprRightVariableSuggestion, value, nameof(SelectedConditionRightExprRightVariableSuggestion), suggestion =>
        {
            if (ShowConditionRightAdvancedPanel && _conditionRightExpr.RightType is ScriptNumericSourceType.VariableReference)
            {
                ConditionRightExprRightValue = suggestion;
            }
        });
    }

    public bool ShowConditionRightAdvancedToggle =>
        ShowConditionFields && IsNumericConditionOperandType(SelectedAction?.ScriptRightOperandType);
    public bool ShowConditionRightAdvancedPanel => ShowConditionRightAdvancedToggle && _conditionRightExpr.IsAdvanced;
    public bool ShowConditionRightSimpleFields => ShowConditionFields && !ShowConditionRightAdvancedPanel;
    public bool ShowConditionRightExprRightVariablePicker =>
        ShowConditionRightAdvancedPanel
        && _conditionRightExpr.RightType is ScriptNumericSourceType.VariableReference
        && HasAvailableVariableNames;

    #endregion

    #region Shared state machine

    private void SetFieldAdvanced(ScriptArithmeticFieldState state, ScriptArithmeticFieldAccess access, bool value)
    {
        var action = SelectedAction;
        if (value && (action is null || !access.OwnsType(action.Type) || !access.IsAvailable(action)))
        {
            value = false;
        }

        if (state.IsAdvanced == value)
        {
            return;
        }

        state.IsAdvanced = value;
        state.Owner = value ? action : null;
        if (action is not null && access.OwnsType(action.Type))
        {
            if (value)
            {
                PrefillArithmeticField(state, access, action);
            }
            else
            {
                DecomposeArithmeticField(state, access, action);
            }
        }

        NotifyScriptArithmeticPresentationChanged();
    }

    private void SetFieldLeftValue(ScriptArithmeticFieldState state, ScriptArithmeticFieldAccess access, string? value)
    {
        var normalized = value ?? string.Empty;
        if (string.Equals(state.LeftValue, normalized, StringComparison.Ordinal))
        {
            return;
        }

        state.LeftValue = normalized;
        ComposeArithmeticField(state, access);
        NotifyScriptArithmeticPresentationChanged();
    }

    private void SetFieldLeftType(ScriptArithmeticFieldState state, ScriptArithmeticFieldAccess access, ScriptNumericSourceType value)
    {
        if (state.LeftType == value)
        {
            return;
        }

        state.LeftType = value;
        ComposeArithmeticField(state, access);
        NotifyScriptArithmeticPresentationChanged();
    }

    private void SetFieldOperator(ScriptArithmeticFieldState state, ScriptArithmeticFieldAccess access, ScriptArithmeticOperation value)
    {
        if (state.Operator == value)
        {
            return;
        }

        state.Operator = value;
        ComposeArithmeticField(state, access);
        NotifyScriptArithmeticPresentationChanged();
    }

    private void SetFieldRightValue(ScriptArithmeticFieldState state, ScriptArithmeticFieldAccess access, string? value)
    {
        var normalized = value ?? string.Empty;
        if (string.Equals(state.RightValue, normalized, StringComparison.Ordinal))
        {
            return;
        }

        state.RightValue = normalized;
        ComposeArithmeticField(state, access);
        NotifyScriptArithmeticPresentationChanged();
    }

    private void SetFieldRightType(ScriptArithmeticFieldState state, ScriptArithmeticFieldAccess access, ScriptNumericSourceType value)
    {
        if (state.RightType == value)
        {
            return;
        }

        state.RightType = value;
        ComposeArithmeticField(state, access);
        NotifyScriptArithmeticPresentationChanged();
    }

    /// <summary>Writes the canonical expression into the model; an empty right operand degrades to the left operand (never stores a dangling operator).</summary>
    private void ComposeArithmeticField(ScriptArithmeticFieldState state, ScriptArithmeticFieldAccess access)
    {
        var action = SelectedAction;
        if (action is null || !state.IsAdvanced || !access.OwnsType(action.Type))
        {
            return;
        }

        access.SetLeftType(action, state.LeftType);

        var leftToken = FormatExpressionOperandToken(state.LeftType, state.LeftValue);
        var rightToken = FormatExpressionOperandToken(state.RightType, state.RightValue);
        if (leftToken.Length is 0 || rightToken.Length is 0)
        {
            access.SetValue(action, ToSimpleStoredForm(state.LeftType, state.LeftValue));
            return;
        }

        access.SetValue(action, ScriptNumericExpression.Format(
            new ScriptNumericExpression(state.LeftType, leftToken, state.Operator, state.RightType, rightToken)));
    }

    /// <summary>Toggle off: drops back to the left operand in simple form (a flipped non-numeric operand type is kept).</summary>
    private static void DecomposeArithmeticField(
        ScriptArithmeticFieldState state,
        ScriptArithmeticFieldAccess access,
        EditorAction action,
        bool writeType = true)
    {
        if (writeType)
        {
            access.SetLeftType(action, state.LeftType);
        }

        access.SetValue(action, ToSimpleStoredForm(state.LeftType, state.LeftValue));
        state.ResetRightOperand();
    }

    private static void PrefillArithmeticField(ScriptArithmeticFieldState state, ScriptArithmeticFieldAccess access, EditorAction action)
    {
        var value = access.GetValue(action);
        if (ScriptNumericExpression.TryParse(value, out var expression) && expression is { Op: not null })
        {
            state.LeftType = expression.LeftSource;
            state.LeftValue = ToSimpleStoredForm(expression.LeftSource, expression.LeftValue);
            state.Operator = expression.Op.Value;
            state.RightType = expression.RightSource;
            state.RightValue = ToSimpleStoredForm(expression.RightSource, expression.RightValue);
            return;
        }

        state.LeftType = access.GetLeftType(action);
        state.LeftValue = value;
        state.ResetRightOperand();
    }

    /// <summary>
    /// Re-derives every field's Advanced state from the given action. Runs on selection
    /// changes so panel state can never leak across actions or action types.
    /// </summary>
    private void SyncScriptArithmeticStateFromModel(EditorAction? action)
    {
        if (action is null)
        {
            _repeatCountExpr.Reset();
            _forStartExpr.Reset();
            _forEndExpr.Reset();
            _forStepExpr.Reset();
            _conditionLeftExpr.Reset();
            _conditionRightExpr.Reset();
            return;
        }

        SyncArithmeticField(_repeatCountExpr, RepeatCountAccess, action);
        SyncArithmeticField(_forStartExpr, ForStartAccess, action);
        SyncArithmeticField(_forEndExpr, ForEndAccess, action);
        SyncArithmeticField(_forStepExpr, ForStepAccess, action);
        SyncArithmeticField(_conditionLeftExpr, ConditionLeftAccess, action);
        SyncArithmeticField(_conditionRightExpr, ConditionRightAccess, action);
    }

    private static void SyncArithmeticField(ScriptArithmeticFieldState state, ScriptArithmeticFieldAccess access, EditorAction action)
    {
        if (!access.OwnsType(action.Type) || !access.IsAvailable(action))
        {
            state.Reset();
            return;
        }

        var value = access.GetValue(action);
        if (ScriptNumericExpression.TryParse(value, out var expression) && expression is { Op: not null })
        {
            state.IsAdvanced = true;
            state.Owner = action;
            PrefillArithmeticField(state, access, action);
            return;
        }

        state.Reset();
    }

    /// <summary>
    /// Normalization on action type / operand type changes: the shared
    /// <see cref="EditorAction.ScriptNumericValue"/> field must never keep an expression when
    /// the action becomes an inc/dec/mul/div amount, and a condition operand flipped to a
    /// non-numeric type drops back to its left operand instead of hiding a live expression.
    /// The decomposition only runs when the field state belongs to this very action
    /// (a type flip of the selected action), never when selection merely moves onto an
    /// existing inc/dec/mul/div action.
    /// </summary>
    private void NormalizeScriptArithmeticForAction(EditorAction action)
    {
        if (action.Type is EditorActionType.IncrementVariable or EditorActionType.DecrementVariable
                or EditorActionType.MultiplyVariable or EditorActionType.DivideVariable
            && _repeatCountExpr.IsAdvanced
            && ReferenceEquals(_repeatCountExpr.Owner, action))
        {
            _repeatCountExpr.IsAdvanced = false;
            _repeatCountExpr.Owner = null;
            DecomposeArithmeticField(_repeatCountExpr, RepeatCountAccess, action);
        }

        if (action.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart)
        {
            if (!IsNumericConditionOperandType(action.ScriptLeftOperandType)
                && _conditionLeftExpr.IsAdvanced
                && ReferenceEquals(_conditionLeftExpr.Owner, action))
            {
                _conditionLeftExpr.IsAdvanced = false;
                _conditionLeftExpr.Owner = null;
                DecomposeArithmeticField(_conditionLeftExpr, ConditionLeftAccess, action, writeType: false);
            }

            if (!IsNumericConditionOperandType(action.ScriptRightOperandType)
                && _conditionRightExpr.IsAdvanced
                && ReferenceEquals(_conditionRightExpr.Owner, action))
            {
                _conditionRightExpr.IsAdvanced = false;
                _conditionRightExpr.Owner = null;
                DecomposeArithmeticField(_conditionRightExpr, ConditionRightAccess, action, writeType: false);
            }
        }
    }

    private void NotifyScriptArithmeticPresentationChanged()
    {
        OnPropertyChanged(nameof(IsRepeatCountAdvanced));
        OnPropertyChanged(nameof(ShowRepeatCountAdvancedToggle));
        OnPropertyChanged(nameof(ShowRepeatCountAdvancedPanel));
        OnPropertyChanged(nameof(ShowRepeatCountSimpleFields));
        OnPropertyChanged(nameof(RepeatCountExprLeftValue));
        OnPropertyChanged(nameof(RepeatCountExprLeftType));
        OnPropertyChanged(nameof(RepeatCountExprOperator));
        OnPropertyChanged(nameof(RepeatCountExprRightValue));
        OnPropertyChanged(nameof(RepeatCountExprRightType));
        OnPropertyChanged(nameof(ShowRepeatCountExprRightVariablePicker));
        OnPropertyChanged(nameof(SelectedRepeatCountExprRightVariableSuggestion));

        OnPropertyChanged(nameof(IsForStartAdvanced));
        OnPropertyChanged(nameof(ShowForStartAdvancedToggle));
        OnPropertyChanged(nameof(ShowForStartAdvancedPanel));
        OnPropertyChanged(nameof(ShowForStartSimpleFields));
        OnPropertyChanged(nameof(ForStartExprLeftValue));
        OnPropertyChanged(nameof(ForStartExprLeftType));
        OnPropertyChanged(nameof(ForStartExprOperator));
        OnPropertyChanged(nameof(ForStartExprRightValue));
        OnPropertyChanged(nameof(ForStartExprRightType));
        OnPropertyChanged(nameof(ShowForStartExprRightVariablePicker));
        OnPropertyChanged(nameof(SelectedForStartExprRightVariableSuggestion));

        OnPropertyChanged(nameof(IsForEndAdvanced));
        OnPropertyChanged(nameof(ShowForEndAdvancedToggle));
        OnPropertyChanged(nameof(ShowForEndAdvancedPanel));
        OnPropertyChanged(nameof(ShowForEndSimpleFields));
        OnPropertyChanged(nameof(ForEndExprLeftValue));
        OnPropertyChanged(nameof(ForEndExprLeftType));
        OnPropertyChanged(nameof(ForEndExprOperator));
        OnPropertyChanged(nameof(ForEndExprRightValue));
        OnPropertyChanged(nameof(ForEndExprRightType));
        OnPropertyChanged(nameof(ShowForEndExprRightVariablePicker));
        OnPropertyChanged(nameof(SelectedForEndExprRightVariableSuggestion));

        OnPropertyChanged(nameof(IsForStepAdvanced));
        OnPropertyChanged(nameof(ShowForStepAdvancedToggle));
        OnPropertyChanged(nameof(ShowForStepAdvancedPanel));
        OnPropertyChanged(nameof(ShowForStepSimpleFields));
        OnPropertyChanged(nameof(ForStepExprLeftValue));
        OnPropertyChanged(nameof(ForStepExprLeftType));
        OnPropertyChanged(nameof(ForStepExprOperator));
        OnPropertyChanged(nameof(ForStepExprRightValue));
        OnPropertyChanged(nameof(ForStepExprRightType));
        OnPropertyChanged(nameof(ShowForStepExprRightVariablePicker));
        OnPropertyChanged(nameof(SelectedForStepExprRightVariableSuggestion));

        OnPropertyChanged(nameof(IsConditionLeftAdvanced));
        OnPropertyChanged(nameof(ShowConditionLeftAdvancedToggle));
        OnPropertyChanged(nameof(ShowConditionLeftAdvancedPanel));
        OnPropertyChanged(nameof(ShowConditionLeftSimpleFields));
        OnPropertyChanged(nameof(ConditionLeftExprLeftValue));
        OnPropertyChanged(nameof(ConditionLeftExprLeftType));
        OnPropertyChanged(nameof(ConditionLeftExprOperator));
        OnPropertyChanged(nameof(ConditionLeftExprRightValue));
        OnPropertyChanged(nameof(ConditionLeftExprRightType));
        OnPropertyChanged(nameof(ShowConditionLeftExprRightVariablePicker));
        OnPropertyChanged(nameof(SelectedConditionLeftExprRightVariableSuggestion));

        OnPropertyChanged(nameof(IsConditionRightAdvanced));
        OnPropertyChanged(nameof(ShowConditionRightAdvancedToggle));
        OnPropertyChanged(nameof(ShowConditionRightAdvancedPanel));
        OnPropertyChanged(nameof(ShowConditionRightSimpleFields));
        OnPropertyChanged(nameof(ConditionRightExprLeftValue));
        OnPropertyChanged(nameof(ConditionRightExprLeftType));
        OnPropertyChanged(nameof(ConditionRightExprOperator));
        OnPropertyChanged(nameof(ConditionRightExprRightValue));
        OnPropertyChanged(nameof(ConditionRightExprRightType));
        OnPropertyChanged(nameof(ShowConditionRightExprRightVariablePicker));
        OnPropertyChanged(nameof(SelectedConditionRightExprRightVariableSuggestion));
    }

    private static string FormatExpressionOperandToken(ScriptNumericSourceType sourceType, string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length is 0
            ? string.Empty
            : EditorActionScriptTokens.FormatNumericToken(sourceType, trimmed, defaultValue: string.Empty);
    }

    private static string ToSimpleStoredForm(ScriptNumericSourceType sourceType, string value)
    {
        var trimmed = value.Trim();
        return sourceType is ScriptNumericSourceType.VariableReference
            ? EditorActionScriptTokens.NormalizeVariableToken(trimmed)
            : trimmed;
    }

    private static bool IsNumericConditionOperandType(ScriptOperandType? operandType)
    {
        return operandType is ScriptOperandType.Number or ScriptOperandType.VariableReference;
    }

    private static ScriptNumericSourceType ToNumericSourceType(ScriptOperandType operandType)
    {
        return operandType is ScriptOperandType.VariableReference
            ? ScriptNumericSourceType.VariableReference
            : ScriptNumericSourceType.Number;
    }

    private static ScriptOperandType ToOperandType(ScriptNumericSourceType sourceType)
    {
        return sourceType is ScriptNumericSourceType.VariableReference
            ? ScriptOperandType.VariableReference
            : ScriptOperandType.Number;
    }

    private sealed class ScriptArithmeticFieldState
    {
        public bool IsAdvanced { get; set; }

        /// <summary>The action instance this state was derived from; null when idle.</summary>
        public EditorAction? Owner { get; set; }

        public string LeftValue { get; set; } = string.Empty;
        public ScriptNumericSourceType LeftType { get; set; } = ScriptNumericSourceType.Number;
        public ScriptArithmeticOperation Operator { get; set; } = ScriptArithmeticOperation.Add;
        public string RightValue { get; set; } = string.Empty;
        public ScriptNumericSourceType RightType { get; set; } = ScriptNumericSourceType.Number;

        public void Reset()
        {
            IsAdvanced = false;
            Owner = null;
            LeftValue = string.Empty;
            LeftType = ScriptNumericSourceType.Number;
            ResetRightOperand();
        }

        public void ResetRightOperand()
        {
            Operator = ScriptArithmeticOperation.Add;
            RightValue = string.Empty;
            RightType = ScriptNumericSourceType.Number;
        }
    }

    private sealed class ScriptArithmeticFieldAccess(
        Func<EditorActionType, bool> ownsType,
        Func<EditorAction, string> getValue,
        Action<EditorAction, string> setValue,
        Func<EditorAction, ScriptNumericSourceType> getLeftType,
        Action<EditorAction, ScriptNumericSourceType> setLeftType,
        Func<EditorAction, bool>? isAvailable = null)
    {
        public bool OwnsType(EditorActionType type) => ownsType(type);
        public string GetValue(EditorAction action) => getValue(action);
        public void SetValue(EditorAction action, string value) => setValue(action, value);
        public ScriptNumericSourceType GetLeftType(EditorAction action) => getLeftType(action);
        public void SetLeftType(EditorAction action, ScriptNumericSourceType type) => setLeftType(action, type);
        public bool IsAvailable(EditorAction action) => isAvailable?.Invoke(action) ?? true;
    }

    #endregion
}
