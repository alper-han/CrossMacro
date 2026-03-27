using CrossMacro.Core.Models;
using FluentAssertions;

namespace CrossMacro.Core.Tests.Models;

public class EditorActionTests
{
    [Fact]
    public void Clone_CreatesNewActionWithCopiedFields()
    {
        // Arrange
        var source = new EditorAction
        {
            Type = EditorActionType.MouseMove,
            X = 40,
            Y = 55,
            IsAbsolute = false,
            Button = MouseButton.Right,
            KeyCode = 30,
            KeyName = "A",
            DelayMs = 25,
            UseRandomDelay = true,
            RandomDelayMinMs = 50,
            RandomDelayMaxMs = 150,
            ScrollAmount = -2,
            Text = "hello",
            ScriptVariableName = "counter",
            ScriptValueType = ScriptValueType.Number,
            ScriptValue = "42",
            ScriptNumericSourceType = ScriptNumericSourceType.VariableReference,
            ScriptNumericValue = "stepAmount",
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptLeftOperand = "counter",
            ScriptConditionOperator = ScriptConditionOperator.LessThanOrEqual,
            ScriptRightOperandType = ScriptOperandType.Number,
            ScriptRightOperand = "100",
            ForVariableName = "i",
            ForStartType = ScriptNumericSourceType.Number,
            ForStartValue = "0",
            ForEndType = ScriptNumericSourceType.Number,
            ForEndValue = "10",
            ForHasStep = true,
            ForStepType = ScriptNumericSourceType.Number,
            ForStepValue = "2"
        };

        // Act
        var clone = source.Clone();

        // Assert
        clone.Should().NotBeSameAs(source);
        clone.Id.Should().NotBe(source.Id);
        clone.Type.Should().Be(source.Type);
        clone.X.Should().Be(source.X);
        clone.Y.Should().Be(source.Y);
        clone.IsAbsolute.Should().Be(source.IsAbsolute);
        clone.Button.Should().Be(source.Button);
        clone.KeyCode.Should().Be(source.KeyCode);
        clone.KeyName.Should().Be(source.KeyName);
        clone.DelayMs.Should().Be(source.DelayMs);
        clone.UseRandomDelay.Should().Be(source.UseRandomDelay);
        clone.RandomDelayMinMs.Should().Be(source.RandomDelayMinMs);
        clone.RandomDelayMaxMs.Should().Be(source.RandomDelayMaxMs);
        clone.ScrollAmount.Should().Be(source.ScrollAmount);
        clone.Text.Should().Be(source.Text);
        clone.ScriptVariableName.Should().Be(source.ScriptVariableName);
        clone.ScriptValueType.Should().Be(source.ScriptValueType);
        clone.ScriptValue.Should().Be(source.ScriptValue);
        clone.ScriptNumericSourceType.Should().Be(source.ScriptNumericSourceType);
        clone.ScriptNumericValue.Should().Be(source.ScriptNumericValue);
        clone.ScriptLeftOperandType.Should().Be(source.ScriptLeftOperandType);
        clone.ScriptLeftOperand.Should().Be(source.ScriptLeftOperand);
        clone.ScriptConditionOperator.Should().Be(source.ScriptConditionOperator);
        clone.ScriptRightOperandType.Should().Be(source.ScriptRightOperandType);
        clone.ScriptRightOperand.Should().Be(source.ScriptRightOperand);
        clone.ForVariableName.Should().Be(source.ForVariableName);
        clone.ForStartType.Should().Be(source.ForStartType);
        clone.ForStartValue.Should().Be(source.ForStartValue);
        clone.ForEndType.Should().Be(source.ForEndType);
        clone.ForEndValue.Should().Be(source.ForEndValue);
        clone.ForHasStep.Should().Be(source.ForHasStep);
        clone.ForStepType.Should().Be(source.ForStepType);
        clone.ForStepValue.Should().Be(source.ForStepValue);
    }

    [Fact]
    public void DisplayName_ForTextInput_TruncatesLongText()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = "abcdefghijklmnopqrstuvwxyz"
        };

        // Act
        var display = action.DisplayName;

        // Assert
        display.Should().Contain("Type");
        display.Should().Contain("...");
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForInvalidDelayAndScroll()
    {
        // Arrange
        var delay = new EditorAction { Type = EditorActionType.Delay, DelayMs = -1 };
        var randomDelay = new EditorAction
        {
            Type = EditorActionType.Delay,
            UseRandomDelay = true,
            RandomDelayMinMs = 200,
            RandomDelayMaxMs = 100
        };
        var scroll = new EditorAction { Type = EditorActionType.ScrollVertical, ScrollAmount = 0 };

        // Assert
        delay.IsValid().Should().BeFalse();
        randomDelay.IsValid().Should().BeFalse();
        scroll.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenScriptVariableReferencesUseDollarPrefix_ReturnsTrue()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.SetVariable,
            ScriptVariableName = "$target",
            ScriptValueType = ScriptValueType.VariableReference,
            ScriptValue = "$source"
        };

        // Act + Assert
        action.IsValid().Should().BeTrue();
    }

    [Fact]
    public void DisplayName_WhenForVariableValuesUseDollarPrefix_DoesNotDuplicateDollar()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.ForBlockStart,
            ForVariableName = "i",
            ForStartType = ScriptNumericSourceType.VariableReference,
            ForStartValue = "$start",
            ForEndType = ScriptNumericSourceType.VariableReference,
            ForEndValue = "$finish",
            ForHasStep = true,
            ForStepType = ScriptNumericSourceType.VariableReference,
            ForStepValue = "$step"
        };

        // Act
        var displayName = action.DisplayName;

        // Assert
        displayName.Should().Contain("$start");
        displayName.Should().Contain("$finish");
        displayName.Should().Contain("$step");
        displayName.Should().NotContain("$$");
    }
}
