namespace CrossMacro.UI.Tests.ViewModels;

public sealed partial class EditorViewModelTests
{

    [Fact]
    public void ScriptConditionOperators_WhenOperandIsColor_FiltersToEqualityOperatorsAndNormalizesSelection()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "color",
            ScriptConditionOperator = ScriptConditionOperator.GreaterThan,
            ScriptRightOperandType = ScriptOperandType.Number,
            ScriptRightOperand = "10",
        };
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.PixelColor, ScreenColorVariableName = "color" });
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _localizationService["Editor_ConditionColorHint"].Returns("Use Color operand.");

        action.ScriptRightOperandType = ScriptOperandType.Color;

        _ = action.ScriptConditionOperator.Should().Be(ScriptConditionOperator.Equals);
        _ = _viewModel.ScriptConditionOperators.Should().Equal(
            ScriptConditionOperator.Equals,
            ScriptConditionOperator.NotEquals);
        _ = _viewModel.ConditionRightOperandHint.Should().Contain("Color");
    }

    [Fact]
    public void ScriptConditionOperators_WhenVariableCanBeNumeric_AllowsNumericOperators()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "found_x",
            ScriptConditionOperator = ScriptConditionOperator.GreaterThan,
            ScriptRightOperandType = ScriptOperandType.Number,
            ScriptRightOperand = "10",
        };
        _viewModel.Actions.Add(new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y",
        });
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = _viewModel.ScriptConditionOperators.Should().Contain(ScriptConditionOperator.GreaterThan);
        _ = action.ScriptConditionOperator.Should().Be(ScriptConditionOperator.GreaterThan);
    }

    [Fact]
    public void ScriptConditionOperators_WhenBooleanVariableSelected_FiltersToEqualityOperators()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "found",
            ScriptConditionOperator = ScriptConditionOperator.GreaterThan,
            ScriptRightOperandType = ScriptOperandType.Boolean,
            ScriptRightOperand = "true",
        };
        _viewModel.Actions.Add(new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y",
        });
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = action.ScriptConditionOperator.Should().Be(ScriptConditionOperator.Equals);
        _ = _viewModel.ScriptConditionOperators.Should().Equal(
            ScriptConditionOperator.Equals,
            ScriptConditionOperator.NotEquals);
    }

    [Fact]
    public void ConditionOperandTextBoxes_WhenVariablePickerIsAvailable_AreHiddenForVariableOperands()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "color",
            ScriptRightOperandType = ScriptOperandType.VariableReference,
        };
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.PixelColor, ScreenColorVariableName = "color" });
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowConditionLeftVariablePicker.Should().BeTrue();
        _ = _viewModel.ShowConditionLeftOperandTextBox.Should().BeFalse();
        _ = _viewModel.SelectedConditionLeftVariableSuggestion.Should().Be("color");
        _ = _viewModel.ShowConditionRightVariablePicker.Should().BeTrue();
        _ = _viewModel.ShowConditionRightOperandTextBox.Should().BeFalse();
    }

    [Fact]
    public void ConditionColorPickers_WhenOperandsAreNotColor_AreHidden()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.Text,
            ScriptRightOperandType = ScriptOperandType.VariableReference,
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowConditionLeftColorPicker.Should().BeFalse();
        _ = _viewModel.ShowConditionRightColorPicker.Should().BeFalse();
    }

    [Fact]
    public void AvailableVariableNames_WhenSetActionsExist_ReturnsNamesFromPreviousActions()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        _viewModel.SelectedAction!.ScriptVariableName = "speed";

        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        _viewModel.SelectedAction!.ScriptVariableName = "mode";

        // Act
        var names = _viewModel.AvailableVariableNames;

        // Assert
        _ = names.Should().Contain("speed");
        _ = names.Should().Contain("mode");
    }

    [Fact]
    public void AvailableVariableNames_WhenSingleSetActionExists_IncludesCurrentVariable()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        _viewModel.SelectedAction!.ScriptVariableName = "i";

        // Act
        var names = _viewModel.AvailableVariableNames;

        // Assert
        _ = names.Should().Contain("i");
        _ = _viewModel.HasAvailableVariableNames.Should().BeTrue();
    }

    [Fact]
    public void AvailableVariableNames_WhenWindowCommandProducesOutput_IncludesWindowVariable()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.WindowCommand,
            WindowCommandMode = WindowCommandMode.Active,
            WindowOutputVariable = "activeTitle",
        };
        _viewModel.Actions.Add(action);

        _ = _viewModel.AvailableVariableNames.Should().Contain("activeTitle");

        action.WindowOutputVariable = "activeClass";

        _ = _viewModel.AvailableVariableNames.Should().Contain("activeClass");
        _ = _viewModel.AvailableVariableNames.Should().NotContain("activeTitle");
    }

    [Fact]
    public void SetVariableSuggestions_WhenSelectingSecondAction_DoesNotMutateFirstAction()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        var first = _viewModel.SelectedAction!;
        first.ScriptVariableName = "alpha";

        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        var second = _viewModel.SelectedAction!;
        second.ScriptVariableName = "beta";

        _viewModel.SelectedAction = second;

        // Act
        _viewModel.SelectedSetVariableSuggestion = "alpha";

        // Assert
        _ = first.ScriptVariableName.Should().Be("alpha");
        _ = second.ScriptVariableName.Should().Be("alpha");
    }

    [Fact]
    public void VariableNameChangeOnOtherAction_DoesNotOverwriteCurrentSelection()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        var first = _viewModel.SelectedAction!;
        first.ScriptVariableName = "one";

        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        var second = _viewModel.SelectedAction!;
        second.ScriptVariableName = "two";
        _viewModel.SelectedAction = second;

        // Act
        first.ScriptVariableName = "three";

        // Assert
        _ = second.ScriptVariableName.Should().Be("two");
        _ = _viewModel.AvailableVariableNames.Should().Contain("three");
    }

    [Fact]
    public void SelectedAction_WhenEditingVariableName_RemainsSelected()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        var selected = _viewModel.SelectedAction!;

        // Act
        selected.ScriptVariableName = "i";
        selected.ScriptVariableName = "it";
        selected.ScriptVariableName = "iter";

        // Assert
        _ = _viewModel.SelectedAction.Should().BeSameAs(selected);
        _ = _viewModel.SelectedActionListItem.Should().NotBeNull();
        _ = _viewModel.SelectedActionListItem!.Action.Should().BeSameAs(selected);
    }
}
