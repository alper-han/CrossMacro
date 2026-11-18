namespace CrossMacro.UI.Tests.ViewModels;

public sealed partial class EditorViewModelTests
{
    #region Advanced toggle visibility gating

    [Fact]
    public void AdvancedToggle_WhenRepeatSelected_ShowsOnlyRepeatCountToggle()
    {
        _viewModel.Picker.ActionType = EditorActionType.RepeatBlockStart;

        _viewModel.AddAction();

        _ = _viewModel.Arithmetic.RepeatCount.ShowAdvancedToggle.Should().BeTrue();
        _ = _viewModel.Arithmetic.ForStart.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ForEnd.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ForStep.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ConditionLeft.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ConditionRight.ShowAdvancedToggle.Should().BeFalse();
    }

    [Fact]
    public void AdvancedToggle_WhenForSelected_ShowsStartAndEndToggles()
    {
        _viewModel.Picker.ActionType = EditorActionType.ForBlockStart;

        _viewModel.AddAction();

        _ = _viewModel.Arithmetic.ForStart.ShowAdvancedToggle.Should().BeTrue();
        _ = _viewModel.Arithmetic.ForEnd.ShowAdvancedToggle.Should().BeTrue();
        _ = _viewModel.Arithmetic.ForStep.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.RepeatCount.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ConditionLeft.ShowAdvancedToggle.Should().BeFalse();
    }

    [Fact]
    public void AdvancedToggle_WhenForStepEnabled_ShowsStepToggle()
    {
        _viewModel.Picker.ActionType = EditorActionType.ForBlockStart;
        _viewModel.AddAction();

        _viewModel.SelectedAction!.ForHasStep = true;

        _ = _viewModel.Arithmetic.ForStep.ShowAdvancedToggle.Should().BeTrue();
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

        _ = _viewModel.Arithmetic.ConditionLeft.ShowAdvancedToggle.Should().BeTrue();
        _ = _viewModel.Arithmetic.ConditionRight.ShowAdvancedToggle.Should().BeTrue();
        _ = _viewModel.Arithmetic.RepeatCount.ShowAdvancedToggle.Should().BeFalse();
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

        _ = _viewModel.Arithmetic.ConditionLeft.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ConditionRight.ShowAdvancedToggle.Should().BeFalse();
    }

    [Theory]
    [InlineData(EditorActionType.IncrementVariable)]
    [InlineData(EditorActionType.DecrementVariable)]
    [InlineData(EditorActionType.MultiplyVariable)]
    [InlineData(EditorActionType.DivideVariable)]
    public void AdvancedToggle_WhenIncDecMulDivSelected_NeverShown(EditorActionType actionType)
    {
        _viewModel.Picker.ActionType = actionType;

        _viewModel.AddAction();

        _ = _viewModel.Fields.ShowIncDecFields.Should().BeTrue();
        _ = _viewModel.Arithmetic.RepeatCount.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ForStart.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ForEnd.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ForStep.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ConditionLeft.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ConditionRight.ShowAdvancedToggle.Should().BeFalse();
    }

    [Theory]
    [InlineData(EditorActionType.MouseMove)]
    [InlineData(EditorActionType.Delay)]
    [InlineData(EditorActionType.SetVariable)]
    [InlineData(EditorActionType.TextInput)]
    public void AdvancedToggle_WhenOtherActionSelected_NeverShown(EditorActionType actionType)
    {
        _viewModel.Picker.ActionType = actionType;

        _viewModel.AddAction();

        _ = _viewModel.Arithmetic.RepeatCount.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ForStart.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ForEnd.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ForStep.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ConditionLeft.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Arithmetic.ConditionRight.ShowAdvancedToggle.Should().BeFalse();
    }

    #endregion

    #region Composition / decomposition round-trips

    [Fact]
    public void RepeatCountAdvanced_ComposingOperands_WritesCanonicalExpression()
    {
        _viewModel.Picker.ActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptNumericSourceType = ScriptNumericSourceType.VariableReference;
        action.ScriptNumericValue = "count";

        _viewModel.Arithmetic.RepeatCount.IsAdvanced = true;
        _viewModel.Arithmetic.RepeatCount.Operator = ScriptArithmeticOperation.Divide;
        _viewModel.Arithmetic.RepeatCount.RightType = ScriptNumericSourceType.Number;
        _viewModel.Arithmetic.RepeatCount.RightValue = "10";

        _ = action.ScriptNumericValue.Should().Be("$count / 10");
        _ = action.ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = _viewModel.Arithmetic.RepeatCount.ShowAdvancedPanel.Should().BeTrue();
        _ = _viewModel.Arithmetic.RepeatCount.ShowSimpleFields.Should().BeFalse();
    }

    [Fact]
    public void RepeatCountAdvanced_ToggleOff_RestoresLeftOperandInSimpleForm()
    {
        _viewModel.Picker.ActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptNumericSourceType = ScriptNumericSourceType.VariableReference;
        action.ScriptNumericValue = "count";
        _viewModel.Arithmetic.RepeatCount.IsAdvanced = true;
        _viewModel.Arithmetic.RepeatCount.Operator = ScriptArithmeticOperation.Divide;
        _viewModel.Arithmetic.RepeatCount.RightValue = "10";
        _ = action.ScriptNumericValue.Should().Be("$count / 10");

        _viewModel.Arithmetic.RepeatCount.IsAdvanced = false;

        _ = action.ScriptNumericValue.Should().Be("count");
        _ = action.ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = _viewModel.Arithmetic.RepeatCount.ShowAdvancedPanel.Should().BeFalse();
        _ = _viewModel.Arithmetic.RepeatCount.ShowSimpleFields.Should().BeTrue();
    }

    [Fact]
    public void RepeatCountAdvanced_ToggleOff_PreservesNumberLeftOperand()
    {
        _viewModel.Picker.ActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptNumericSourceType = ScriptNumericSourceType.Number;
        action.ScriptNumericValue = "10";
        _viewModel.Arithmetic.RepeatCount.IsAdvanced = true;
        _viewModel.Arithmetic.RepeatCount.Operator = ScriptArithmeticOperation.Add;
        _viewModel.Arithmetic.RepeatCount.RightType = ScriptNumericSourceType.VariableReference;
        _viewModel.Arithmetic.RepeatCount.RightValue = "extra";
        _ = action.ScriptNumericValue.Should().Be("10 + $extra");

        _viewModel.Arithmetic.RepeatCount.IsAdvanced = false;

        _ = action.ScriptNumericValue.Should().Be("10");
        _ = action.ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.Number);
    }

    [Fact]
    public void RepeatCountAdvanced_ToggleOn_PrefillsLeftOperandFromSimpleValue()
    {
        _viewModel.Picker.ActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptNumericSourceType = ScriptNumericSourceType.Number;
        action.ScriptNumericValue = "10";

        _viewModel.Arithmetic.RepeatCount.IsAdvanced = true;

        _ = _viewModel.Arithmetic.RepeatCount.LeftValue.Should().Be("10");
        _ = _viewModel.Arithmetic.RepeatCount.LeftType.Should().Be(ScriptNumericSourceType.Number);
        _ = _viewModel.Arithmetic.RepeatCount.Operator.Should().Be(ScriptArithmeticOperation.Add);
        _ = _viewModel.Arithmetic.RepeatCount.RightValue.Should().BeEmpty();
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

        _ = _viewModel.Arithmetic.RepeatCount.IsAdvanced.Should().BeTrue();
        _ = _viewModel.Arithmetic.RepeatCount.LeftValue.Should().Be("count");
        _ = _viewModel.Arithmetic.RepeatCount.LeftType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = _viewModel.Arithmetic.RepeatCount.Operator.Should().Be(ScriptArithmeticOperation.Divide);
        _ = _viewModel.Arithmetic.RepeatCount.RightValue.Should().Be("10");
        _ = _viewModel.Arithmetic.RepeatCount.RightType.Should().Be(ScriptNumericSourceType.Number);
    }

    [Fact]
    public void RepeatCountAdvanced_WhenSimpleValueEditedDirectly_PanelStaysClosed()
    {
        _viewModel.Picker.ActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;

        action.ScriptNumericValue = "7";

        _ = _viewModel.Arithmetic.RepeatCount.IsAdvanced.Should().BeFalse();
        _ = _viewModel.Arithmetic.RepeatCount.ShowSimpleFields.Should().BeTrue();
        _ = _viewModel.Arithmetic.RepeatCount.ShowAdvancedPanel.Should().BeFalse();
    }

    [Fact]
    public void ForEndAdvanced_ComposingOperands_WritesCanonicalExpression()
    {
        _viewModel.Picker.ActionType = EditorActionType.ForBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ForVariableName = "i";
        action.ForEndType = ScriptNumericSourceType.VariableReference;
        action.ForEndValue = "n";

        _viewModel.Arithmetic.ForEnd.IsAdvanced = true;
        _viewModel.Arithmetic.ForEnd.Operator = ScriptArithmeticOperation.Add;
        _viewModel.Arithmetic.ForEnd.RightValue = "1";

        _ = action.ForEndValue.Should().Be("$n + 1");
        _ = action.ForEndType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = _viewModel.Arithmetic.ForStart.IsAdvanced.Should().BeFalse();
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

        _ = _viewModel.Arithmetic.ForEnd.IsAdvanced.Should().BeTrue();
        _ = _viewModel.Arithmetic.ForEnd.LeftValue.Should().Be("n");
        _ = _viewModel.Arithmetic.ForEnd.Operator.Should().Be(ScriptArithmeticOperation.Add);
        _ = _viewModel.Arithmetic.ForEnd.RightValue.Should().Be("1");
        _ = _viewModel.Arithmetic.ForStep.IsAdvanced.Should().BeTrue();
        _ = _viewModel.Arithmetic.ForStep.LeftValue.Should().Be("s");
        _ = _viewModel.Arithmetic.ForStep.Operator.Should().Be(ScriptArithmeticOperation.Subtract);
        _ = _viewModel.Arithmetic.ForStep.RightValue.Should().Be("1");
        _ = _viewModel.Arithmetic.ForStart.IsAdvanced.Should().BeFalse();
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

        _viewModel.Arithmetic.ConditionLeft.IsAdvanced = true;
        _viewModel.Arithmetic.ConditionLeft.Operator = ScriptArithmeticOperation.Add;
        _viewModel.Arithmetic.ConditionLeft.RightValue = "1";

        _ = action.ScriptLeftOperand.Should().Be("$x + 1");
        _ = action.ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        _ = EditorActionScriptTokens.ValidateOperandToken(action.ScriptLeftOperandType, action.ScriptLeftOperand).Should().BeTrue();
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

        _ = _viewModel.Arithmetic.ConditionRight.IsAdvanced.Should().BeTrue();
        _ = _viewModel.Arithmetic.ConditionRight.LeftValue.Should().Be("1");
        _ = _viewModel.Arithmetic.ConditionRight.LeftOperandType.Should().Be(ScriptOperandType.Number);
        _ = _viewModel.Arithmetic.ConditionRight.Operator.Should().Be(ScriptArithmeticOperation.Add);
        _ = _viewModel.Arithmetic.ConditionRight.RightValue.Should().Be("2");
        _ = _viewModel.Arithmetic.ConditionLeft.IsAdvanced.Should().BeFalse();
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
        _ = _viewModel.Arithmetic.ConditionLeft.IsAdvanced.Should().BeTrue();

        _viewModel.Arithmetic.ConditionLeft.IsAdvanced = false;

        _ = action.ScriptLeftOperand.Should().Be("x");
        _ = action.ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        _ = EditorActionScriptTokens.ValidateOperandToken(action.ScriptLeftOperandType, action.ScriptLeftOperand).Should().BeTrue();
    }

    #endregion

    #region Operator list

    [Fact]
    public void ScriptArithmeticOperators_ContainsExactlyFourOperationsWithoutModulo()
    {
        _ = ScriptArithmeticEditor.Operators.Should().Equal(
            ScriptArithmeticOperation.Add,
            ScriptArithmeticOperation.Subtract,
            ScriptArithmeticOperation.Multiply,
            ScriptArithmeticOperation.Divide);
        _ = ScriptArithmeticEditor.Operators.Should().NotContain(ScriptArithmeticOperation.Modulo);
    }

    #endregion

    #region Adversarial: malformed input, stale state

    [Fact]
    public void RepeatCountAdvanced_WhenRightOperandIsGarbage_ComposesWithoutCrashAndValidationRejects()
    {
        _viewModel.Picker.ActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptNumericSourceType = ScriptNumericSourceType.VariableReference;
        action.ScriptNumericValue = "count";

        _viewModel.Arithmetic.RepeatCount.IsAdvanced = true;
        _viewModel.Arithmetic.RepeatCount.RightValue = "abc";

        _ = action.ScriptNumericValue.Should().Be("$count + abc");
        _ = EditorActionScriptTokens.ValidateBlockNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue).Should().BeFalse();
        _ = _viewModel.Arithmetic.RepeatCount.ShowAdvancedPanel.Should().BeTrue();
    }

    [Fact]
    public void IncDecMulDivAmount_WhenExpressionTyped_ValidationRejects()
    {
        _viewModel.Picker.ActionType = EditorActionType.MultiplyVariable;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptVariableName = "x";
        action.ScriptNumericSourceType = ScriptNumericSourceType.VariableReference;

        action.ScriptNumericValue = "$x + 1";

        // Amounts never accept arithmetic; the Advanced affordance does not exist for them.
        _ = EditorActionScriptTokens.ValidateNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue).Should().BeFalse();
        _ = _viewModel.Arithmetic.RepeatCount.ShowAdvancedToggle.Should().BeFalse();
    }

    [Fact]
    public void RepeatCountAdvanced_WhenActionTypeFlipsToIncrement_DecomposesToLeftOperand()
    {
        _viewModel.Picker.ActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScriptVariableName = "count";
        action.ScriptNumericSourceType = ScriptNumericSourceType.VariableReference;
        action.ScriptNumericValue = "count";
        _viewModel.Arithmetic.RepeatCount.IsAdvanced = true;
        _viewModel.Arithmetic.RepeatCount.Operator = ScriptArithmeticOperation.Divide;
        _viewModel.Arithmetic.RepeatCount.RightValue = "10";
        _ = action.ScriptNumericValue.Should().Be("$count / 10");

        action.Type = EditorActionType.IncrementVariable;

        _ = _viewModel.Arithmetic.RepeatCount.IsAdvanced.Should().BeFalse();
        _ = _viewModel.Arithmetic.RepeatCount.ShowAdvancedToggle.Should().BeFalse();
        _ = _viewModel.Fields.ShowIncDecFields.Should().BeTrue();
        _ = action.ScriptNumericValue.Should().Be("count");
        _ = action.ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = EditorActionScriptTokens.ValidateNumericToken(action.ScriptNumericSourceType, action.ScriptNumericValue).Should().BeTrue();

        action.Type = EditorActionType.RepeatBlockStart;

        _ = _viewModel.Arithmetic.RepeatCount.ShowAdvancedToggle.Should().BeTrue();
        _ = _viewModel.Arithmetic.RepeatCount.IsAdvanced.Should().BeFalse();
        _ = action.ScriptNumericValue.Should().Be("count");
    }

    [Fact]
    public void AdvancedState_WhenSelectionMovesToAnotherAction_DoesNotLeak()
    {
        _viewModel.Picker.ActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        var repeat = _viewModel.SelectedAction!;
        repeat.ScriptNumericSourceType = ScriptNumericSourceType.VariableReference;
        repeat.ScriptNumericValue = "count";
        _viewModel.Arithmetic.RepeatCount.IsAdvanced = true;
        _viewModel.Arithmetic.RepeatCount.RightValue = "10";

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
        _ = _viewModel.Arithmetic.RepeatCount.IsAdvanced.Should().BeFalse();
        _ = _viewModel.Arithmetic.RepeatCount.ShowAdvancedToggle.Should().BeFalse();

        _viewModel.SelectedAction = repeat;

        _ = _viewModel.Arithmetic.RepeatCount.IsAdvanced.Should().BeTrue();
        _ = _viewModel.Arithmetic.RepeatCount.RightValue.Should().Be("10");
        _ = _viewModel.Arithmetic.RepeatCount.Operator.Should().Be(ScriptArithmeticOperation.Add);
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
        _viewModel.Arithmetic.ConditionLeft.IsAdvanced = true;
        _viewModel.Arithmetic.ConditionLeft.RightValue = "1";
        _ = action.ScriptLeftOperand.Should().Be("$x + 1");

        action.ScriptLeftOperandType = ScriptOperandType.Text;

        _ = _viewModel.Arithmetic.ConditionLeft.IsAdvanced.Should().BeFalse();
        _ = _viewModel.Arithmetic.ConditionLeft.ShowAdvancedToggle.Should().BeFalse();
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

        _viewModel.Arithmetic.ConditionLeft.IsAdvanced = true;

        _ = _viewModel.Arithmetic.ConditionLeft.IsAdvanced.Should().BeFalse();
        _ = action.ScriptLeftOperand.Should().Be("hello");
    }

    #endregion
}
