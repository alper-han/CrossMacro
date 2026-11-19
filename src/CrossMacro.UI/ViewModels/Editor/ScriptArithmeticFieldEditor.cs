namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// Advanced arithmetic editing of one block-argument numeric field (repeat count, for start/end/step or a
/// condition operand). The canonical expression string lives in the model value; this editor only
/// decomposes and recomposes it via <see cref="ScriptNumericExpression"/>.
/// </summary>
public sealed class ScriptArithmeticFieldEditor : ObservableObject
{
    private readonly ScriptArithmeticEditor _host;
    private readonly ScriptArithmeticFieldAccess _access;
    private readonly Func<bool> _isGroupVisible;
    private readonly ScriptArithmeticFieldState _state = new();

    internal ScriptArithmeticFieldEditor(
        ScriptArithmeticEditor host,
        ScriptArithmeticFieldAccess access,
        Func<bool> isGroupVisible,
        VariableSuggestionScope suggestionScope)
    {
        _host = host;
        _access = access;
        _isGroupVisible = isGroupVisible;
        RightVariableSuggestion = new VariableSuggestionSelector(suggestionScope, suggestion =>
        {
            if (ShowAdvancedPanel && _state.RightType is ScriptNumericSourceType.VariableReference)
            {
                RightValue = suggestion;
            }
        });
    }

    public VariableSuggestionSelector RightVariableSuggestion { get; }

    public bool IsAdvanced
    {
        get => _state.IsAdvanced;
        set => SetAdvanced(value);
    }

    public string LeftValue
    {
        get => _state.LeftValue;
        set => Update(_state.LeftValue, value ?? string.Empty, static (state, next) => state.LeftValue = next);
    }

    public ScriptNumericSourceType LeftType
    {
        get => _state.LeftType;
        set => Update(_state.LeftType, value, static (state, next) => state.LeftType = next);
    }

    public ScriptArithmeticOperation Operator
    {
        get => _state.Operator;
        set => Update(_state.Operator, value, static (state, next) => state.Operator = next);
    }

    public string RightValue
    {
        get => _state.RightValue;
        set => Update(_state.RightValue, value ?? string.Empty, static (state, next) => state.RightValue = next);
    }

    public ScriptNumericSourceType RightType
    {
        get => _state.RightType;
        set => Update(_state.RightType, value, static (state, next) => state.RightType = next);
    }

    /// <summary>Operand-typed view of <see cref="LeftType"/> for condition operands.</summary>
    public ScriptOperandType LeftOperandType
    {
        get => ScriptArithmeticEditor.ToOperandType(_state.LeftType);
        set => LeftType = ScriptArithmeticEditor.ToNumericSourceType(value);
    }

    /// <summary>Operand-typed view of <see cref="RightType"/> for condition operands.</summary>
    public ScriptOperandType RightOperandType
    {
        get => ScriptArithmeticEditor.ToOperandType(_state.RightType);
        set => RightType = ScriptArithmeticEditor.ToNumericSourceType(value);
    }

    public bool ShowAdvancedToggle => _isGroupVisible() && _host.SelectedAction is { } action && _access.IsAvailable(action);

    public bool ShowAdvancedPanel => ShowAdvancedToggle && _state.IsAdvanced;

    public bool ShowSimpleFields => _isGroupVisible() && !ShowAdvancedPanel;

    public bool ShowRightVariablePicker =>
        ShowAdvancedPanel
        && _state.RightType is ScriptNumericSourceType.VariableReference
        && _host.HasAvailableVariableNames;

    internal void NotifyChanged()
    {
        OnPropertyChanged(nameof(IsAdvanced));
        OnPropertyChanged(nameof(ShowAdvancedToggle));
        OnPropertyChanged(nameof(ShowAdvancedPanel));
        OnPropertyChanged(nameof(ShowSimpleFields));
        OnPropertyChanged(nameof(LeftValue));
        OnPropertyChanged(nameof(LeftType));
        OnPropertyChanged(nameof(LeftOperandType));
        OnPropertyChanged(nameof(Operator));
        OnPropertyChanged(nameof(RightValue));
        OnPropertyChanged(nameof(RightType));
        OnPropertyChanged(nameof(RightOperandType));
        OnPropertyChanged(nameof(ShowRightVariablePicker));
        RightVariableSuggestion.NotifyChanged();
    }

    internal void ClearSuggestion() => RightVariableSuggestion.Clear();

    internal void Reset() => _state.Reset();

    /// <summary>Re-derives the Advanced state from the action so panel state never leaks across actions.</summary>
    internal void SyncFromModel(EditorAction action)
    {
        if (!_access.OwnsType(action.Type) || !_access.IsAvailable(action))
        {
            _state.Reset();
            return;
        }

        var value = _access.GetValue(action);
        if (ScriptNumericExpression.TryParse(value, out var expression) && expression is { Op: not null })
        {
            _state.IsAdvanced = true;
            _state.Owner = action;
            Prefill(action);
            return;
        }

        _state.Reset();
    }

    /// <summary>
    /// Drops an expression back to its left operand when the action it belongs to stops being able to hold
    /// one: an action flipped to inc/dec/mul/div, or a condition operand flipped to a non-numeric type.
    /// Only runs when this state was derived from this very action, never when selection merely moves.
    /// </summary>
    internal void CollapseIfUnsupported(EditorAction action, bool writeType)
    {
        if (!_state.IsAdvanced || !ReferenceEquals(_state.Owner, action))
        {
            return;
        }

        _state.IsAdvanced = false;
        _state.Owner = null;
        Decompose(action, writeType);
    }

    private void SetAdvanced(bool value)
    {
        var action = _host.SelectedAction;
        if (value && (action is null || !_access.OwnsType(action.Type) || !_access.IsAvailable(action)))
        {
            value = false;
        }

        if (_state.IsAdvanced == value)
        {
            return;
        }

        _state.IsAdvanced = value;
        _state.Owner = value ? action : null;
        if (action is not null && _access.OwnsType(action.Type))
        {
            if (value)
            {
                Prefill(action);
            }
            else
            {
                Decompose(action, writeType: true);
            }
        }

        _host.NotifyChanged();
    }

    private void Update<T>(T current, T next, Action<ScriptArithmeticFieldState, T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(current, next))
        {
            return;
        }

        assign(_state, next);
        Compose();
        _host.NotifyChanged();
    }

    /// <summary>Writes the canonical expression into the model; an empty right operand degrades to the left operand (never stores a dangling operator).</summary>
    private void Compose()
    {
        var action = _host.SelectedAction;
        if (action is null || !_state.IsAdvanced || !_access.OwnsType(action.Type))
        {
            return;
        }

        _access.SetLeftType(action, _state.LeftType);

        var leftToken = ScriptArithmeticEditor.FormatExpressionOperandToken(_state.LeftType, _state.LeftValue);
        var rightToken = ScriptArithmeticEditor.FormatExpressionOperandToken(_state.RightType, _state.RightValue);
        if (leftToken.Length is 0 || rightToken.Length is 0)
        {
            _access.SetValue(action, ScriptArithmeticEditor.ToSimpleStoredForm(_state.LeftType, _state.LeftValue));
            return;
        }

        _access.SetValue(action, ScriptNumericExpression.Format(
            new ScriptNumericExpression(_state.LeftType, leftToken, _state.Operator, _state.RightType, rightToken)));
    }

    /// <summary>Toggle off: drops back to the left operand in simple form (a flipped non-numeric operand type is kept).</summary>
    private void Decompose(EditorAction action, bool writeType)
    {
        if (writeType)
        {
            _access.SetLeftType(action, _state.LeftType);
        }

        _access.SetValue(action, ScriptArithmeticEditor.ToSimpleStoredForm(_state.LeftType, _state.LeftValue));
        _state.ResetRightOperand();
    }

    private void Prefill(EditorAction action)
    {
        var value = _access.GetValue(action);
        if (ScriptNumericExpression.TryParse(value, out var expression) && expression is { Op: not null })
        {
            _state.LeftType = expression.LeftSource;
            _state.LeftValue = ScriptArithmeticEditor.ToSimpleStoredForm(expression.LeftSource, expression.LeftValue);
            _state.Operator = expression.Op.Value;
            _state.RightType = expression.RightSource;
            _state.RightValue = ScriptArithmeticEditor.ToSimpleStoredForm(expression.RightSource, expression.RightValue);
            return;
        }

        _state.LeftType = _access.GetLeftType(action);
        _state.LeftValue = value;
        _state.ResetRightOperand();
    }
}
