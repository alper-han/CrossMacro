
namespace CrossMacro.UI.Tests.ViewModels;

public sealed partial class EditorViewModelTests
{
    #region Advanced toggle visibility gating

    [Fact]
    public void AdvancedToggle_WhenRepeatSelected_ShowsOnlyRepeatCountToggle()
    {
        _viewModel.NewActionType = EditorActionType.RepeatBlockStart;

        _viewModel.AddAction();

        _ = _viewModel.ShowRepeatCountAdvancedToggle.Should().BeTrue();
        _ = _viewModel.ShowForStartAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowForEndAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowForStepAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowConditionLeftAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowConditionRightAdvancedToggle.Should().BeFalse();
    }

    [Fact]
    public void AdvancedToggle_WhenForSelected_ShowsStartAndEndToggles()
    {
        _viewModel.NewActionType = EditorActionType.ForBlockStart;

        _viewModel.AddAction();

        _ = _viewModel.ShowForStartAdvancedToggle.Should().BeTrue();
        _ = _viewModel.ShowForEndAdvancedToggle.Should().BeTrue();
        _ = _viewModel.ShowForStepAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowRepeatCountAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowConditionLeftAdvancedToggle.Should().BeFalse();
    }

    [Fact]
    public void AdvancedToggle_WhenForStepEnabled_ShowsStepToggle()
    {
        _viewModel.NewActionType = EditorActionType.ForBlockStart;
        _viewModel.AddAction();

        _viewModel.SelectedAction!.ForHasStep = true;

        _ = _viewModel.ShowForStepAdvancedToggle.Should().BeTrue();
    }

    [Theory]
    [InlineData(EditorActionType.IfBlockStart)]
    [InlineData(EditorActionType.WhileBlockStart)]
    public void AdvancedToggle_WhenConditionSelected_ShowsForNumericOperands(EditorActionType actionType)
    {
        var action = new EditorAction
        {
            Type = actionType,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "x",
            ScriptConditionOperator = ScriptConditionOperator.GreaterThan,
            ScriptRightOperandType = ScriptOperandType.Number,
            ScriptRightOperand = "5",
        };
        _viewModel.Actions.Add(action);

        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowConditionLeftAdvancedToggle.Should().BeTrue();
        _ = _viewModel.ShowConditionRightAdvancedToggle.Should().BeTrue();
        _ = _viewModel.ShowRepeatCountAdvancedToggle.Should().BeFalse();
    }

    [Theory]
    [InlineData(ScriptOperandType.Text)]
    [InlineData(ScriptOperandType.Boolean)]
    [InlineData(ScriptOperandType.Color)]
    public void AdvancedToggle_WhenConditionOperandNotNumeric_NeverShown(ScriptOperandType operandType)
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = operandType,
            ScriptLeftOperand = "true",
            ScriptConditionOperator = ScriptConditionOperator.Equals,
            ScriptRightOperandType = ScriptOperandType.Boolean,
            ScriptRightOperand = "true",
        };
        _viewModel.Actions.Add(action);

        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowConditionLeftAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowConditionRightAdvancedToggle.Should().BeFalse();
    }

    [Theory]
    [InlineData(EditorActionType.IncrementVariable)]
    [InlineData(EditorActionType.DecrementVariable)]
    [InlineData(EditorActionType.MultiplyVariable)]
    [InlineData(EditorActionType.DivideVariable)]
    public void AdvancedToggle_WhenIncDecMulDivSelected_NeverShown(EditorActionType actionType)
    {
        _viewModel.NewActionType = actionType;

        _viewModel.AddAction();

        _ = _viewModel.ShowIncDecFields.Should().BeTrue();
        _ = _viewModel.ShowRepeatCountAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowForStartAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowForEndAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowForStepAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowConditionLeftAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowConditionRightAdvancedToggle.Should().BeFalse();
    }

    [Theory]
    [InlineData(EditorActionType.MouseMove)]
    [InlineData(EditorActionType.Delay)]
    [InlineData(EditorActionType.SetVariable)]
    [InlineData(EditorActionType.TextInput)]
    public void AdvancedToggle_WhenOtherActionSelected_NeverShown(EditorActionType actionType)
    {
        _viewModel.NewActionType = actionType;

        _viewModel.AddAction();

        _ = _viewModel.ShowRepeatCountAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowForStartAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowForEndAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowForStepAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowConditionLeftAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowConditionRightAdvancedToggle.Should().BeFalse();
    }

    #endregion

    #region Composition / decomposition round-trips

    [Fact]
    public void RepeatCountAdvanced_ComposingOperands_WritesCanonicalExpression()
    {
        _viewModel.NewActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptNumericSourceType = ScriptNumericSourceType.VariableReference;
        action.ScriptNumericValue = "count";

        _viewModel.IsRepeatCountAdvanced = true;
        _viewModel.RepeatCountExprOperator = ScriptArithmeticOperation.Divide;
        _viewModel.RepeatCountExprRightType = ScriptNumericSourceType.Number;
        _viewModel.RepeatCountExprRightValue = "10";

        _ = action.ScriptNumericValue.Should().Be("$count / 10");
        _ = action.ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = _viewModel.ShowRepeatCountAdvancedPanel.Should().BeTrue();
        _ = _viewModel.ShowRepeatCountSimpleFields.Should().BeFalse();
    }

    [Fact]
    public void RepeatCountAdvanced_ToggleOff_RestoresLeftOperandInSimpleForm()
    {
        _viewModel.NewActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptNumericSourceType = ScriptNumericSourceType.VariableReference;
        action.ScriptNumericValue = "count";
        _viewModel.IsRepeatCountAdvanced = true;
        _viewModel.RepeatCountExprOperator = ScriptArithmeticOperation.Divide;
        _viewModel.RepeatCountExprRightValue = "10";
        _ = action.ScriptNumericValue.Should().Be("$count / 10");

        _viewModel.IsRepeatCountAdvanced = false;

        _ = action.ScriptNumericValue.Should().Be("count");
        _ = action.ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = _viewModel.ShowRepeatCountAdvancedPanel.Should().BeFalse();
        _ = _viewModel.ShowRepeatCountSimpleFields.Should().BeTrue();
    }

    [Fact]
    public void RepeatCountAdvanced_ToggleOff_PreservesNumberLeftOperand()
    {
        _viewModel.NewActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptNumericSourceType = ScriptNumericSourceType.Number;
        action.ScriptNumericValue = "10";
        _viewModel.IsRepeatCountAdvanced = true;
        _viewModel.RepeatCountExprOperator = ScriptArithmeticOperation.Add;
        _viewModel.RepeatCountExprRightType = ScriptNumericSourceType.VariableReference;
        _viewModel.RepeatCountExprRightValue = "extra";
        _ = action.ScriptNumericValue.Should().Be("10 + $extra");

        _viewModel.IsRepeatCountAdvanced = false;

        _ = action.ScriptNumericValue.Should().Be("10");
        _ = action.ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.Number);
    }

    [Fact]
    public void RepeatCountAdvanced_ToggleOn_PrefillsLeftOperandFromSimpleValue()
    {
        _viewModel.NewActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptNumericSourceType = ScriptNumericSourceType.Number;
        action.ScriptNumericValue = "10";

        _viewModel.IsRepeatCountAdvanced = true;

        _ = _viewModel.RepeatCountExprLeftValue.Should().Be("10");
        _ = _viewModel.RepeatCountExprLeftType.Should().Be(ScriptNumericSourceType.Number);
        _ = _viewModel.RepeatCountExprOperator.Should().Be(ScriptArithmeticOperation.Add);
        _ = _viewModel.RepeatCountExprRightValue.Should().BeEmpty();
        // Toggling on alone must not rewrite the stored value.
        _ = action.ScriptNumericValue.Should().Be("10");
    }

    [Fact]
    public void RepeatCountAdvanced_WhenActionHoldsExpression_PrefillsPanelOnSelection()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.RepeatBlockStart,
            ScriptNumericSourceType = ScriptNumericSourceType.VariableReference,
            ScriptNumericValue = "$count / 10",
        };
        _viewModel.Actions.Add(action);

        _viewModel.SelectedAction = action;

        _ = _viewModel.IsRepeatCountAdvanced.Should().BeTrue();
        _ = _viewModel.RepeatCountExprLeftValue.Should().Be("count");
        _ = _viewModel.RepeatCountExprLeftType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = _viewModel.RepeatCountExprOperator.Should().Be(ScriptArithmeticOperation.Divide);
        _ = _viewModel.RepeatCountExprRightValue.Should().Be("10");
        _ = _viewModel.RepeatCountExprRightType.Should().Be(ScriptNumericSourceType.Number);
    }

    [Fact]
    public void RepeatCountAdvanced_WhenSimpleValueEditedDirectly_PanelStaysClosed()
    {
        _viewModel.NewActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;

        action.ScriptNumericValue = "7";

        _ = _viewModel.IsRepeatCountAdvanced.Should().BeFalse();
        _ = _viewModel.ShowRepeatCountSimpleFields.Should().BeTrue();
        _ = _viewModel.ShowRepeatCountAdvancedPanel.Should().BeFalse();
    }

    [Fact]
    public void ForEndAdvanced_ComposingOperands_WritesCanonicalExpression()
    {
        _viewModel.NewActionType = EditorActionType.ForBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ForVariableName = "i";
        action.ForEndType = ScriptNumericSourceType.VariableReference;
        action.ForEndValue = "n";

        _viewModel.IsForEndAdvanced = true;
        _viewModel.ForEndExprOperator = ScriptArithmeticOperation.Add;
        _viewModel.ForEndExprRightValue = "1";

        _ = action.ForEndValue.Should().Be("$n + 1");
        _ = action.ForEndType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = _viewModel.IsForStartAdvanced.Should().BeFalse();
    }

    [Fact]
    public void ForStepAdvanced_WhenActionHoldsExpression_PrefillsPanelOnSelection()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.ForBlockStart,
            ForVariableName = "i",
            ForStartType = ScriptNumericSourceType.Number,
            ForStartValue = "0",
            ForEndType = ScriptNumericSourceType.VariableReference,
            ForEndValue = "$n + 1",
            ForHasStep = true,
            ForStepType = ScriptNumericSourceType.VariableReference,
            ForStepValue = "$s - 1",
        };
        _viewModel.Actions.Add(action);

        _viewModel.SelectedAction = action;

        _ = _viewModel.IsForEndAdvanced.Should().BeTrue();
        _ = _viewModel.ForEndExprLeftValue.Should().Be("n");
        _ = _viewModel.ForEndExprOperator.Should().Be(ScriptArithmeticOperation.Add);
        _ = _viewModel.ForEndExprRightValue.Should().Be("1");
        _ = _viewModel.IsForStepAdvanced.Should().BeTrue();
        _ = _viewModel.ForStepExprLeftValue.Should().Be("s");
        _ = _viewModel.ForStepExprOperator.Should().Be(ScriptArithmeticOperation.Subtract);
        _ = _viewModel.ForStepExprRightValue.Should().Be("1");
        _ = _viewModel.IsForStartAdvanced.Should().BeFalse();
    }

    [Fact]
    public void ConditionLeftAdvanced_ComposingOperands_WritesCanonicalExpression()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "x",
            ScriptConditionOperator = ScriptConditionOperator.GreaterThan,
            ScriptRightOperandType = ScriptOperandType.Number,
            ScriptRightOperand = "5",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _viewModel.IsConditionLeftAdvanced = true;
        _viewModel.ConditionLeftExprOperator = ScriptArithmeticOperation.Add;
        _viewModel.ConditionLeftExprRightValue = "1";

        _ = action.ScriptLeftOperand.Should().Be("$x + 1");
        _ = action.ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        _ = action.IsValid().Should().BeTrue();
    }

    [Fact]
    public void ConditionRightAdvanced_WhenActionHoldsExpression_PrefillsPanelOnSelection()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.WhileBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "n",
            ScriptConditionOperator = ScriptConditionOperator.GreaterThan,
            ScriptRightOperandType = ScriptOperandType.Number,
            ScriptRightOperand = "1 + 2",
        };
        _viewModel.Actions.Add(action);

        _viewModel.SelectedAction = action;

        _ = _viewModel.IsConditionRightAdvanced.Should().BeTrue();
        _ = _viewModel.ConditionRightExprLeftValue.Should().Be("1");
        _ = _viewModel.ConditionRightExprLeftType.Should().Be(ScriptOperandType.Number);
        _ = _viewModel.ConditionRightExprOperator.Should().Be(ScriptArithmeticOperation.Add);
        _ = _viewModel.ConditionRightExprRightValue.Should().Be("2");
        _ = _viewModel.IsConditionLeftAdvanced.Should().BeFalse();
    }

    [Fact]
    public void ConditionLeftAdvanced_ToggleOff_RestoresLeftOperandInSimpleForm()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "$x + 1",
            ScriptConditionOperator = ScriptConditionOperator.GreaterThan,
            ScriptRightOperandType = ScriptOperandType.Number,
            ScriptRightOperand = "5",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _viewModel.IsConditionLeftAdvanced.Should().BeTrue();

        _viewModel.IsConditionLeftAdvanced = false;

        _ = action.ScriptLeftOperand.Should().Be("x");
        _ = action.ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        _ = action.IsValid().Should().BeTrue();
    }

    #endregion

    #region Operator list

    [Fact]
    public void ScriptArithmeticOperators_ContainsExactlyFourOperationsWithoutModulo()
    {
        _ = EditorViewModel.ScriptArithmeticOperators.Should().Equal(
            ScriptArithmeticOperation.Add,
            ScriptArithmeticOperation.Subtract,
            ScriptArithmeticOperation.Multiply,
            ScriptArithmeticOperation.Divide);
        _ = EditorViewModel.ScriptArithmeticOperators.Should().NotContain(ScriptArithmeticOperation.Modulo);
    }

    #endregion

    #region Adversarial: malformed input, stale state

    [Fact]
    public void RepeatCountAdvanced_WhenRightOperandIsGarbage_ComposesWithoutCrashAndValidationRejects()
    {
        _viewModel.NewActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptNumericSourceType = ScriptNumericSourceType.VariableReference;
        action.ScriptNumericValue = "count";

        _viewModel.IsRepeatCountAdvanced = true;
        _viewModel.RepeatCountExprRightValue = "abc";

        _ = action.ScriptNumericValue.Should().Be("$count + abc");
        _ = action.IsValid().Should().BeFalse();
        _ = _viewModel.ShowRepeatCountAdvancedPanel.Should().BeTrue();
    }

    [Fact]
    public void IncDecMulDivAmount_WhenExpressionTyped_ValidationRejects()
    {
        _viewModel.NewActionType = EditorActionType.MultiplyVariable;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptVariableName = "x";
        action.ScriptNumericSourceType = ScriptNumericSourceType.VariableReference;

        action.ScriptNumericValue = "$x + 1";

        // Amounts never accept arithmetic; the Advanced affordance does not exist for them.
        _ = action.IsValid().Should().BeFalse();
        _ = _viewModel.ShowRepeatCountAdvancedToggle.Should().BeFalse();
    }

    [Fact]
    public void RepeatCountAdvanced_WhenActionTypeFlipsToIncrement_DecomposesToLeftOperand()
    {
        _viewModel.NewActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptVariableName = "count";
        action.ScriptNumericSourceType = ScriptNumericSourceType.VariableReference;
        action.ScriptNumericValue = "count";
        _viewModel.IsRepeatCountAdvanced = true;
        _viewModel.RepeatCountExprOperator = ScriptArithmeticOperation.Divide;
        _viewModel.RepeatCountExprRightValue = "10";
        _ = action.ScriptNumericValue.Should().Be("$count / 10");

        action.Type = EditorActionType.IncrementVariable;

        _ = _viewModel.IsRepeatCountAdvanced.Should().BeFalse();
        _ = _viewModel.ShowRepeatCountAdvancedToggle.Should().BeFalse();
        _ = _viewModel.ShowIncDecFields.Should().BeTrue();
        _ = action.ScriptNumericValue.Should().Be("count");
        _ = action.ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = action.IsValid().Should().BeTrue();

        action.Type = EditorActionType.RepeatBlockStart;

        _ = _viewModel.ShowRepeatCountAdvancedToggle.Should().BeTrue();
        _ = _viewModel.IsRepeatCountAdvanced.Should().BeFalse();
        _ = action.ScriptNumericValue.Should().Be("count");
    }

    [Fact]
    public void AdvancedState_WhenSelectionMovesToAnotherAction_DoesNotLeak()
    {
        _viewModel.NewActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var repeat = _viewModel.SelectedAction!;
        repeat.ScriptNumericSourceType = ScriptNumericSourceType.VariableReference;
        repeat.ScriptNumericValue = "count";
        _viewModel.IsRepeatCountAdvanced = true;
        _viewModel.RepeatCountExprRightValue = "10";

        var increment = new EditorAction
        {
            Type = EditorActionType.IncrementVariable,
            ScriptVariableName = "x",
            ScriptNumericSourceType = ScriptNumericSourceType.Number,
            ScriptNumericValue = "2",
        };
        _viewModel.Actions.Add(increment);

        _viewModel.SelectedAction = increment;

        // The shared ScriptNumericValue field of the increment action must stay untouched.
        _ = increment.ScriptNumericValue.Should().Be("2");
        _ = _viewModel.IsRepeatCountAdvanced.Should().BeFalse();
        _ = _viewModel.ShowRepeatCountAdvancedToggle.Should().BeFalse();

        _viewModel.SelectedAction = repeat;

        _ = _viewModel.IsRepeatCountAdvanced.Should().BeTrue();
        _ = _viewModel.RepeatCountExprRightValue.Should().Be("10");
        _ = _viewModel.RepeatCountExprOperator.Should().Be(ScriptArithmeticOperation.Add);
    }

    [Fact]
    public void ConditionAdvanced_WhenOperandTypeFlipsToNonNumeric_DropsBackToLeftOperand()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "x",
            ScriptConditionOperator = ScriptConditionOperator.Equals,
            ScriptRightOperandType = ScriptOperandType.Boolean,
            ScriptRightOperand = "true",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _viewModel.IsConditionLeftAdvanced = true;
        _viewModel.ConditionLeftExprRightValue = "1";
        _ = action.ScriptLeftOperand.Should().Be("$x + 1");

        action.ScriptLeftOperandType = ScriptOperandType.Text;

        _ = _viewModel.IsConditionLeftAdvanced.Should().BeFalse();
        _ = _viewModel.ShowConditionLeftAdvancedToggle.Should().BeFalse();
        _ = action.ScriptLeftOperand.Should().Be("x");
    }

    [Fact]
    public void ConditionAdvanced_WhenSetDirectlyOnNonNumericOperand_IsRefused()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.Text,
            ScriptLeftOperand = "hello",
            ScriptConditionOperator = ScriptConditionOperator.Equals,
            ScriptRightOperandType = ScriptOperandType.Text,
            ScriptRightOperand = "world",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _viewModel.IsConditionLeftAdvanced = true;

        _ = _viewModel.IsConditionLeftAdvanced.Should().BeFalse();
        _ = action.ScriptLeftOperand.Should().Be("hello");
    }

    #endregion
}
