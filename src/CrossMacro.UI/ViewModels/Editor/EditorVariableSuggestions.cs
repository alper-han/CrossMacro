namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// The variable names an editor offers in its pickers (all variables, and color variables for the
/// selected action) together with every picker that applies a chosen name to the selected action.
/// </summary>
public sealed class EditorVariableSuggestions : ObservableObject
{
    private readonly EditorViewModel _owner;
    private readonly VariableSuggestionSelector[] _pickers;

    internal EditorVariableSuggestions(EditorViewModel owner, VariableSuggestionScope scope)
    {
        _owner = owner;
        SetVariable = new(scope, name =>
        {
            if (Selected?.Type is EditorActionType.SetVariable)
            {
                Selected.ScriptVariableName = name;
            }
        });
        IncDec = new(scope, name =>
        {
            if (Selected?.Type is EditorActionType.IncrementVariable or EditorActionType.DecrementVariable
                or EditorActionType.MultiplyVariable or EditorActionType.DivideVariable)
            {
                Selected.ScriptVariableName = name;
            }
        });
        ConditionLeft = new(
            scope,
            name =>
            {
                if (Selected?.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart
                    && Selected.ScriptLeftOperandType is ScriptOperandType.VariableReference)
                {
                    Selected.ScriptLeftOperand = name;
                }
            },
            fallback => ProjectConditionOperand(fallback, Selected?.ScriptLeftOperandType, Selected?.ScriptLeftOperand));
        ConditionRight = new(
            scope,
            name =>
            {
                if (Selected?.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart
                    && Selected.ScriptRightOperandType is ScriptOperandType.VariableReference)
                {
                    Selected.ScriptRightOperand = name;
                }
            },
            fallback => ProjectConditionOperand(fallback, Selected?.ScriptRightOperandType, Selected?.ScriptRightOperand));
        For = new(scope, name =>
        {
            if (Selected?.Type is EditorActionType.ForBlockStart)
            {
                Selected.ForVariableName = name;
            }
        });
        Clipboard = new(scope, name =>
        {
            if (Selected?.Type is EditorActionType.ClipboardGet or EditorActionType.CopySelectionToVariable)
            {
                Selected.ScriptVariableName = name;
            }
        });
        ScreenTargetColor = new(scope, name =>
        {
            if (Selected?.Type is EditorActionType.WaitColor or EditorActionType.PixelSearch)
            {
                Selected.ScreenTargetColorVariableName = name;
            }
        });
        _pickers = [SetVariable, IncDec, ConditionLeft, ConditionRight, For, Clipboard, ScreenTargetColor];
    }

    public IReadOnlyList<string> AvailableNames { get; private set; } = [];

    public bool HasAvailableNames => AvailableNames.Count > 0;

    public IReadOnlyList<string> AvailableColorNames { get; private set; } = [];

    public bool HasAvailableColorNames => AvailableColorNames.Count > 0;

    public VariableSuggestionSelector SetVariable { get; }

    public VariableSuggestionSelector IncDec { get; }

    public VariableSuggestionSelector ConditionLeft { get; }

    public VariableSuggestionSelector ConditionRight { get; }

    public VariableSuggestionSelector For { get; }

    public VariableSuggestionSelector Clipboard { get; }

    public VariableSuggestionSelector ScreenTargetColor { get; }

    public bool ShowSetVariablePicker => _owner.Fields.ShowSetVariableFields && HasAvailableNames;

    public bool ShowClipboardVariablePicker => (_owner.Fields.ShowClipboardGetFields || _owner.Fields.ShowCopySelectionToVariableFields) && HasAvailableNames;

    public bool ShowIncDecVariablePicker => _owner.Fields.ShowIncDecFields && HasAvailableNames;

    public bool ShowForVariablePicker => _owner.Fields.ShowForFields && HasAvailableNames;

    public bool ShowConditionLeftVariablePicker =>
        _owner.Fields.ShowConditionFields
        && HasAvailableNames
        && Selected?.ScriptLeftOperandType is ScriptOperandType.VariableReference;

    public bool ShowConditionLeftOperandTextBox =>
        _owner.Fields.ShowConditionFields
        && (Selected?.ScriptLeftOperandType is not ScriptOperandType.VariableReference || !ShowConditionLeftVariablePicker);

    public bool ShowConditionLeftColorPicker =>
        _owner.Fields.ShowConditionFields
        && !_owner.IsCapturingConditionLeftColor
        && _owner.IsScreenPixelReaderSupported
        && Selected?.ScriptLeftOperandType is ScriptOperandType.Color;

    public bool ShowConditionRightVariablePicker =>
        _owner.Fields.ShowConditionFields
        && HasAvailableNames
        && Selected?.ScriptRightOperandType is ScriptOperandType.VariableReference;

    public bool ShowConditionRightOperandTextBox =>
        _owner.Fields.ShowConditionFields
        && (Selected?.ScriptRightOperandType is not ScriptOperandType.VariableReference || !ShowConditionRightVariablePicker);

    public bool ShowConditionRightColorPicker =>
        _owner.Fields.ShowConditionFields
        && !_owner.IsCapturingConditionRightColor
        && _owner.IsScreenPixelReaderSupported
        && Selected?.ScriptRightOperandType is ScriptOperandType.Color;

    private EditorAction? Selected => _owner.SelectedAction;

    /// <summary>Recomputes the offered names from the action list; returns whether either list changed.</summary>
    internal bool Refresh()
    {
        var next = EditorVariableCatalog.GetNames(_owner.Actions);
        var nextColor = EditorVariableCatalog.GetColorNames(_owner.Actions, Selected);
        var namesChanged = !AvailableNames.SequenceEqual(next, StringComparer.Ordinal);
        var colorNamesChanged = !AvailableColorNames.SequenceEqual(nextColor, StringComparer.Ordinal);

        if (namesChanged)
        {
            AvailableNames = next;
            OnPropertyChanged(nameof(AvailableNames));
            OnPropertyChanged(nameof(HasAvailableNames));
        }

        if (colorNamesChanged)
        {
            AvailableColorNames = nextColor;
            OnPropertyChanged(nameof(AvailableColorNames));
            OnPropertyChanged(nameof(HasAvailableColorNames));
        }

        NotifyChanged();
        ClearSelections();
        return namesChanged || colorNamesChanged;
    }

    /// <summary>Re-raises every picker flag and projected selection that depends on the selected action.</summary>
    internal void NotifyChanged()
    {
        OnPropertyChanged(nameof(ShowSetVariablePicker));
        OnPropertyChanged(nameof(ShowClipboardVariablePicker));
        OnPropertyChanged(nameof(ShowIncDecVariablePicker));
        OnPropertyChanged(nameof(ShowForVariablePicker));
        OnPropertyChanged(nameof(ShowConditionLeftVariablePicker));
        OnPropertyChanged(nameof(ShowConditionLeftOperandTextBox));
        OnPropertyChanged(nameof(ShowConditionLeftColorPicker));
        OnPropertyChanged(nameof(ShowConditionRightVariablePicker));
        OnPropertyChanged(nameof(ShowConditionRightOperandTextBox));
        OnPropertyChanged(nameof(ShowConditionRightColorPicker));
        Clipboard.NotifyChanged();
        ConditionLeft.NotifyChanged();
        ConditionRight.NotifyChanged();
    }

    internal void NotifyColorPickersChanged()
    {
        OnPropertyChanged(nameof(ShowConditionLeftColorPicker));
        OnPropertyChanged(nameof(ShowConditionRightColorPicker));
    }

    internal void ClearSelections()
    {
        foreach (var picker in _pickers)
        {
            picker.Clear();
        }
    }

    private string? ProjectConditionOperand(string? fallback, ScriptOperandType? operandType, string? operand)
    {
        if (operandType is ScriptOperandType.VariableReference
            && !string.IsNullOrWhiteSpace(operand)
            && AvailableNames.Contains(operand, StringComparer.Ordinal))
        {
            return operand;
        }

        return fallback;
    }
}
