// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class EditorActionConverterTests
{

    [Fact]
    public void ToAndFromMacroSequence_WhenScriptBackedActionUsesRawDeviceSpace_PreservesIt()
    {
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = false,
                CoordinateSpace = MouseCoordinateSpace.RawDevice,
                X = 7,
                Y = -4,
            },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        var sequence = _converter.ToMacroSequence(actions, "Raw relative script", isAbsolute: false);
        var restored = _converter.FromMacroSequence(sequence);

        _ = sequence.ScriptSteps.Should().Equal("repeat 1 {", "move rel-raw 7 -4", "}");
        _ = sequence.Events.Should().ContainSingle();
        _ = sequence.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        _ = sequence.Events[0].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
        _ = restored.Should().HaveCount(3);
        _ = restored[1].IsAbsolute.Should().BeFalse();
        _ = restored[1].CoordinateSpace.Should().Be(MouseCoordinateSpace.RawDevice);
    }

    [Fact]
    public void ToMacroSequence_WhenScriptStepIfElse_CompilesBranch()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.SetVariable, Text = "mode=fast" },
            new EditorAction { Type = EditorActionType.IfBlockStart, Text = "$mode == fast" },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
            new EditorAction { Type = EditorActionType.ElseBlockStart },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Macro", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
        _ = sequence.Events[0].Button.Should().Be(MacroMouseButton.Left);
    }

    [Fact]
    public void ToMacroSequence_WhenOnlyStateScriptActions_ProducesRuntimeOnlyScriptMacro()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "i",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "1",
            },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "State Only Script", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().BeEmpty();
        _ = sequence.ScriptSteps.Should().ContainSingle().Which.Should().StartWith("set i");
    }

    [Fact]
    public void ToMacroSequence_WhenClipboardActionsUsed_PreservesClipboardScriptSteps()
    {
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.ClipboardGet, ScriptVariableName = "clipText" },
            new EditorAction { Type = EditorActionType.ClipboardSet, Text = "hello $clipText" },
        };

        var sequence = _converter.ToMacroSequence(actions, "Clipboard Macro", isAbsolute: true);

        _ = sequence.Events.Should().BeEmpty();
        _ = sequence.ScriptSteps.Should().Equal(
            "clipboard get clipText",
            "clipboard set hello $clipText");
    }

    [Fact]
    public void ToMacroSequence_WhenClipboardSetUsesEscapedDollar_PreservesLiteralDollarEscape()
    {
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.ClipboardSet, Text = "literal $$clipText" },
        };

        var sequence = _converter.ToMacroSequence(actions, "Clipboard Macro", isAbsolute: true);

        _ = sequence.Events.Should().BeEmpty();
        _ = sequence.ScriptSteps.Should().Equal("clipboard set literal $$clipText");
    }

    [Fact]
    public void FromMacroSequenceWithDiagnostics_WhenClipboardStepsPresent_RestoresStructuredActions()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "clipboard get clipText",
                "clipboard set hello $clipText",
            },
        };

        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = result.RestoredFromScriptSteps.Should().BeTrue();
        _ = result.Warnings.Should().BeEmpty();
        _ = result.Actions.Should().HaveCount(2);
        _ = result.Actions[0].Type.Should().Be(EditorActionType.ClipboardGet);
        _ = result.Actions[0].ScriptVariableName.Should().Be("clipText");
        _ = result.Actions[1].Type.Should().Be(EditorActionType.ClipboardSet);
        _ = result.Actions[1].Text.Should().Be("hello $clipText");
    }

    [Fact]
    public void FromMacroSequenceWithDiagnostics_WhenClipboardSetUsesEscapedDollar_PreservesLiteralDollarEscape()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps = { "clipboard set literal $$clipText" },
        };

        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = result.RestoredFromScriptSteps.Should().BeTrue();
        _ = result.Warnings.Should().BeEmpty();
        _ = result.Actions.Should().ContainSingle();
        _ = result.Actions[0].Type.Should().Be(EditorActionType.ClipboardSet);
        _ = result.Actions[0].Text.Should().Be("literal $$clipText");
    }

    [Fact]
    public void ToMacroSequence_WhenScriptAndRegularActionsMixed_UsesUnifiedCompiler()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 120, Y = 220 },
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "2" },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Mixed Macro", isAbsolute: true);

        // Assert
        _ = sequence.IsAbsoluteCoordinates.Should().BeTrue();
        _ = sequence.Events.Should().HaveCount(3);
        _ = sequence.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = sequence.Events[1].Type.Should().Be(EventType.Click);
        _ = sequence.Events[2].Type.Should().Be(EventType.Click);
    }

    [Fact]
    public void ToMacroSequence_WhenStateScriptAndMixedCoordinates_UsesStandardConversionAndPreservesScriptSteps()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "mode",
                ScriptValueType = ScriptValueType.Text,
                ScriptValue = "fast",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = true,
                X = 320,
                Y = 240,
            },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "State Script Mixed Coordinates", isAbsolute: true);

        // Assert
        _ = sequence.Events.Should().HaveCount(2);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
        _ = sequence.Events[0].UseCurrentPosition.Should().BeTrue();
        _ = sequence.Events[0].CoordinateMode.Should().BeNull();
        _ = sequence.Events[1].Type.Should().Be(EventType.MouseMove);
        _ = sequence.Events[1].X.Should().Be(320);
        _ = sequence.Events[1].Y.Should().Be(240);
        _ = sequence.Events[1].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        _ = sequence.ScriptSteps.Should().Equal(
            "set mode fast",
            "click current left",
            "move abs 320 240");
    }

    [Fact]
    public void ToMacroSequence_WhenAbsoluteMovePrecedesCurrentPositionClickInScriptBlock_PreservesCurrentPositionSemantics()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = true,
                X = 500,
                Y = 300,
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Absolute Then Current Click", isAbsolute: true);

        // Assert
        _ = sequence.Events.Should().HaveCount(2);
        _ = sequence.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = sequence.Events[0].X.Should().Be(500);
        _ = sequence.Events[0].Y.Should().Be(300);
        _ = sequence.Events[1].Type.Should().Be(EventType.Click);
        _ = sequence.Events[1].UseCurrentPosition.Should().BeTrue();
        _ = sequence.ScriptSteps.Should().Equal(
            "repeat 1 {",
            "move abs 500 300",
            "click current left",
            "}");
    }

    [Fact]
    public void ToMacroSequence_WhenScriptCompilationFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.WhileBlockStart, Text = "$i < 2" },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        Action act = () => _converter.ToMacroSequence(actions, "Broken Script", isAbsolute: false);

        // Assert
        _ = act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToMacroSequence_WhenStructuredScriptActionsUsed_CompilesSuccessfully()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "mode",
                ScriptValueType = ScriptValueType.Text,
                ScriptValue = "fast",
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "mode",
                ScriptConditionOperator = ScriptConditionOperator.Equals,
                ScriptRightOperandType = ScriptOperandType.Text,
                ScriptRightOperand = "fast",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Structured Script Macro", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
        _ = sequence.Events[0].Button.Should().Be(MacroMouseButton.Left);
    }

    [Fact]
    public void ToMacroSequence_WhenConditionTextOperandsStartWithDollar_EscapesLiteralDollar()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.Text,
                ScriptLeftOperand = "$foo",
                ScriptConditionOperator = ScriptConditionOperator.Equals,
                ScriptRightOperandType = ScriptOperandType.Text,
                ScriptRightOperand = "$foo",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Condition Dollar Text", isAbsolute: false);

        // Assert
        _ = sequence.ScriptSteps.Should().Equal(
            "if $$foo == $$foo {",
            "click current left",
            "}");
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
    }

    [Fact]
    public void ToMacroSequence_WhenStructuredConditionUsesVariableReferenceWithDollarPrefix_NormalizesOnlyVariableSide()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "name",
                ScriptValueType = ScriptValueType.Text,
                ScriptValue = "$foo",
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "$name",
                ScriptConditionOperator = ScriptConditionOperator.Equals,
                ScriptRightOperandType = ScriptOperandType.Text,
                ScriptRightOperand = "$foo",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        var sequence = _converter.ToMacroSequence(actions, "Condition Variable Prefix", isAbsolute: false);

        _ = sequence.ScriptSteps.Should().Equal(
            "set name $$foo",
            "if $name == $$foo {",
            "click current left",
            "}");
        _ = sequence.Events.Should().ContainSingle();
    }

    [Fact]
    public void ToMacroSequence_WhenLegacyScriptTextExistsAndStructuredFieldsAreEdited_PrefersStructuredSerialization()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                Text = "broken_set_payload",
                ScriptVariableName = "counter",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "0",
            },
            new EditorAction
            {
                Type = EditorActionType.IncrementVariable,
                Text = "broken_inc_payload",
                ScriptVariableName = "counter",
                ScriptNumericSourceType = ScriptNumericSourceType.Number,
                ScriptNumericValue = "2",
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                Text = "broken_condition_payload",
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "counter",
                ScriptConditionOperator = ScriptConditionOperator.GreaterThanOrEqual,
                ScriptRightOperandType = ScriptOperandType.Number,
                ScriptRightOperand = "2",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                Text = "broken_for_payload",
                ForVariableName = "j",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "1",
                ForEndType = ScriptNumericSourceType.Number,
                ForEndValue = "2",
                ForHasStep = true,
                ForStepType = ScriptNumericSourceType.Number,
                ForStepValue = "1",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Structured Overrides Legacy Text", isAbsolute: false);

        // Assert
        _ = sequence.ScriptSteps.Should().Equal(
            "set counter 0",
            "inc counter 2",
            "if $counter >= 2 {",
            "click current left",
            "}",
            "for j from 1 to 2 step 1 {",
            "click current left",
            "}");
        _ = sequence.Events.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(EditorActionType.MultiplyVariable, "mul x 2", ScriptNumericSourceType.Number, "2")]
    [InlineData(EditorActionType.DivideVariable, "div x $y", ScriptNumericSourceType.VariableReference, "y")]
    public void ToAndFromMacroSequence_WhenMulDivActionUsed_RoundTripsLosslessly(
        EditorActionType actionType,
        string expectedStep,
        ScriptNumericSourceType expectedAmountSourceType,
        string expectedAmountValue)
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "x",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "1",
            },
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "y",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "3",
            },
            new EditorAction
            {
                Type = actionType,
                ScriptVariableName = "x",
                ScriptNumericSourceType = expectedAmountSourceType,
                ScriptNumericValue = expectedAmountValue,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Mul Div Round Trip", isAbsolute: false);
        var restored = _converter.FromMacroSequence(sequence);

        _ = sequence.ScriptSteps.Should().Equal("set x 1", "set y 3", expectedStep);
        _ = restored.Should().HaveCount(3);
        var restoredAction = restored[2];
        _ = restoredAction.Type.Should().Be(actionType);
        _ = restoredAction.ScriptVariableName.Should().Be("x");
        _ = restoredAction.ScriptNumericSourceType.Should().Be(expectedAmountSourceType);
        _ = restoredAction.ScriptNumericValue.Should().Be(expectedAmountValue);
        _ = restoredAction.PreferLegacyScriptText.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenMulDivStepsAreMalformed_RestoresStructuredActionsWithLegacyText()
    {
        var sequence = new MacroSequence { Name = "Malformed Mul Div" };
        sequence.ScriptSteps.Add("mul 1x 2");
        sequence.ScriptSteps.Add("div x abc");

        var restored = _converter.FromMacroSequence(sequence);

        _ = restored.Should().HaveCount(2);
        _ = restored[0].Type.Should().Be(EditorActionType.MultiplyVariable);
        _ = restored[0].Text.Should().Be("1x 2");
        _ = restored[0].PreferLegacyScriptText.Should().BeTrue();
        _ = restored[1].Type.Should().Be(EditorActionType.DivideVariable);
        _ = restored[1].Text.Should().Be("x abc");
        _ = restored[1].PreferLegacyScriptText.Should().BeTrue();
    }

    [Fact]
    public void ToMacroSequence_WhenStructuredMulDivUsed_ExpandsToExpectedResult()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "x",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "5",
            },
            new EditorAction
            {
                Type = EditorActionType.MultiplyVariable,
                ScriptVariableName = "x",
                ScriptNumericSourceType = ScriptNumericSourceType.Number,
                ScriptNumericValue = "2",
            },
            new EditorAction
            {
                Type = EditorActionType.DivideVariable,
                ScriptVariableName = "x",
                ScriptNumericSourceType = ScriptNumericSourceType.Number,
                ScriptNumericValue = "4",
            },
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = false, X = 0, Y = 0, CoordinateXToken = "$x", CoordinateYToken = "0" },
        };

        var sequence = _converter.ToMacroSequence(actions, "Structured Mul Div", isAbsolute: false);

        _ = sequence.ScriptSteps.Should().Equal("set x 5", "mul x 2", "div x 4", "move rel-logical $x 0");
        _ = sequence.Events.Should().ContainSingle();
        _ = sequence.Events[0].X.Should().Be(2); // (5 * 2) / 4
    }

    [Fact]
    public void ToMacroSequence_WhenStructuredForBlockUsed_RepeatsExpectedCount()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                ForVariableName = "i",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "1",
                ForEndType = ScriptNumericSourceType.Number,
                ForEndValue = "3",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Structured For Macro", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(3);
        _ = sequence.Events.Should().OnlyContain(ev => ev.Type == EventType.Click);
    }

    [Fact]
    public void ToMacroSequence_WhenForEndAndStepShareVariable_CompilesAndNormalizesVariableToken()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "limit",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "3",
            },
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                ForVariableName = "i",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "0",
                ForEndType = ScriptNumericSourceType.VariableReference,
                ForEndValue = "$limit",
                ForHasStep = true,
                ForStepType = ScriptNumericSourceType.VariableReference,
                ForStepValue = "$limit",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Shared For Variable", isAbsolute: false);

        // Assert
        _ = sequence.ScriptSteps.Should().Contain("for i from 0 to $limit step $limit {");
        _ = sequence.Events.Should().HaveCount(2); // i = 0, 3
        _ = sequence.Events.Should().OnlyContain(ev => ev.Type == EventType.Click);
    }

    [Fact]
    public void ToMacroSequence_WhenBreakUsedInsideLoop_StopsLoopExecution()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                ForVariableName = "i",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "1",
                ForEndType = ScriptNumericSourceType.Number,
                ForEndValue = "3",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.Break },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Break Loop Macro", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
        _ = sequence.Events[0].Button.Should().Be(MacroMouseButton.Left);
    }

    [Fact]
    public void ToMacroSequence_WhenContinueUsedInsideLoop_SkipsRemainingBodySteps()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                ForVariableName = "i",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "1",
                ForEndType = ScriptNumericSourceType.Number,
                ForEndValue = "3",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.Continue },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Right, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Continue Loop Macro", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(3);
        _ = sequence.Events.Should().OnlyContain(ev => ev.Type == EventType.Click && ev.Button == MacroMouseButton.Left);
    }

    [Fact]
    public void ToMacroSequence_WhenBreakUsedOutsideLoop_ThrowsInvalidOperationException()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.Break },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
        };

        // Act
        Action act = () => _converter.ToMacroSequence(actions, "Invalid Break Macro", isAbsolute: false);

        // Assert
        _ = act.Should().Throw<InvalidOperationException>()
            .WithMessage("*can only be used inside repeat/while/for blocks*");
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedConversionUsed_UsesSkipInitialZeroZeroDefault()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "i",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "1",
            },
            new EditorAction
            {
                Type = EditorActionType.KeyPress,
                KeyCode = 30,
            },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Skip Initial Propagation", isAbsolute: false, skipInitialZeroZero: false);

        // Assert
        _ = sequence.SkipInitialZeroZero.Should().BeTrue();
    }

    [Fact]
    public void ToMacroSequence_WhenScriptActionsUsed_PreservesSourceScriptSteps()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "i",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "0",
            },
            new EditorAction
            {
                Type = EditorActionType.ForBlockStart,
                ForVariableName = "i",
                ForStartType = ScriptNumericSourceType.Number,
                ForStartValue = "1",
                ForEndType = ScriptNumericSourceType.Number,
                ForEndValue = "3",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Step Preserve", isAbsolute: false);

        // Assert
        _ = sequence.ScriptSteps.Should().Equal(
            "set i 0",
            "for i from 1 to 3 {",
            "click current left",
            "}");
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedContainsRandomDelay_PreservesRandomDelayMetadata()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "i",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "1",
            },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.Delay, UseRandomDelay = true, RandomDelayMinMs = 10, RandomDelayMaxMs = 20 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Random Delay", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(2);
        _ = sequence.Events[1].HasRandomDelay.Should().BeTrue();
        _ = sequence.Events[1].RandomDelayMinMs.Should().Be(10);
        _ = sequence.Events[1].RandomDelayMaxMs.Should().Be(20);
    }

    [Fact]
    public void ToMacroSequence_WhenVariableCoordinateActionHasDelay_PreservesDelayInScriptAndEvent()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "x",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "10",
            },
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "y",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "20",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = true,
                CoordinateXToken = "$x",
                CoordinateYToken = "$y",
                DelayMs = 500,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Variable Coordinate Delay", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            "set x 10",
            "set y 20",
            "delay 500",
            "move abs $x $y");
        _ = sequence.Events.Should().ContainSingle();
        _ = sequence.Events[0].DelayMs.Should().Be(500);
    }

    [Fact]
    public void ToMacroSequence_WhenVariableCoordinateActionHasRandomDelay_PreservesRandomDelayInScript()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "x",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "10",
            },
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "y",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "20",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = true,
                CoordinateXToken = "$x",
                CoordinateYToken = "$y",
                UseRandomDelay = true,
                RandomDelayMinMs = 10,
                RandomDelayMaxMs = 20,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Variable Coordinate Random Delay", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            "set x 10",
            "set y 20",
            "delay random 10 20",
            "move abs $x $y");
        _ = sequence.Events.Should().ContainSingle();
        _ = sequence.Events[0].HasRandomDelay.Should().BeTrue();
        _ = sequence.Events[0].RandomDelayMinMs.Should().Be(10);
        _ = sequence.Events[0].RandomDelayMaxMs.Should().Be(20);
    }

    [Fact]
    public void ToMacroSequence_WhenPixelSearchFeedsDelayedVariableMove_SerializesDelayBeforeMove()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.PixelSearch,
                ScreenLeft = 0,
                ScreenTop = 0,
                ScreenWidth = 100,
                ScreenHeight = 100,
                ScreenColorHex = "142C2D",
                ScreenFoundVariableName = "found",
                ScreenFoundXVariableName = "x",
                ScreenFoundYVariableName = "y",
                ScreenTimeoutMs = 5000,
                ScreenTolerance = 5,
            },
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = true,
                CoordinateXToken = "$x",
                CoordinateYToken = "$y",
                DelayMs = 500,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Runtime Variable Coordinate Delay", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            "pixelsearch 0 0 100 100 142C2D found x y timeout 5000 tolerance 5",
            "delay 500",
            "move abs $x $y");
        _ = sequence.Events.Should().BeEmpty();
    }

    [Fact]
    public void ToMacroSequence_WhenDelayedAbsoluteClickReusesPreviousVariableMove_KeepsDelayAfterMove()
    {
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "x",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "10",
            },
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "y",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "20",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseMove,
                IsAbsolute = true,
                CoordinateXToken = "$x",
                CoordinateYToken = "$y",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                IsAbsolute = true,
                CoordinateXToken = "$x",
                CoordinateYToken = "$y",
                Button = MacroMouseButton.Left,
                DelayMs = 500,
            },
        };

        var sequence = _converter.ToMacroSequence(actions, "Delayed Variable Click", isAbsolute: true);

        _ = sequence.ScriptSteps.Should().Equal(
            "set x 10",
            "set y 20",
            "move abs $x $y",
            "delay 500",
            "click left");
        _ = sequence.Events.Should().HaveCount(2);
        _ = sequence.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = sequence.Events[1].Type.Should().Be(EventType.Click);
        _ = sequence.Events[1].DelayMs.Should().Be(500);
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedHasInitialRandomDelay_PreservesFirstEventRandomDelay()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction { Type = EditorActionType.Delay, UseRandomDelay = true, RandomDelayMinMs = 10, RandomDelayMaxMs = 20 },
            new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Left, UseCurrentPosition = true, IsAbsolute = false },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Initial Random Delay", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(1);
        _ = sequence.Events[0].Type.Should().Be(EventType.Click);
        _ = sequence.Events[0].HasRandomDelay.Should().BeTrue();
        _ = sequence.Events[0].RandomDelayMinMs.Should().Be(10);
        _ = sequence.Events[0].RandomDelayMaxMs.Should().Be(20);
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedModifierOnlyKeyPress_DoesNotFail()
    {
        // Arrange
        _ = _keyCodeMapper.IsModifierKeyCode(29).Returns(returnThis: true);
        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "i",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "1",
            },
            new EditorAction
            {
                Type = EditorActionType.KeyPress,
                KeyCode = 29,
            },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Modifier KeyPress", isAbsolute: false);

        // Assert
        _ = sequence.Events.Should().HaveCount(2);
        _ = sequence.Events[0].Type.Should().Be(EventType.KeyPress);
        _ = sequence.Events[0].KeyCode.Should().Be(29);
        _ = sequence.Events[1].Type.Should().Be(EventType.KeyRelease);
        _ = sequence.Events[1].KeyCode.Should().Be(29);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsPresent_RestoresStructuredScriptActions()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set i 0",
                "for i from 1 to 10 {",
                "click left",
                "}",
                "repeat $n {",
                "tap 30",
                "}",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(7);

        _ = actions[0].Type.Should().Be(EditorActionType.SetVariable);
        _ = actions[0].ScriptVariableName.Should().Be("i");
        _ = actions[0].ScriptValueType.Should().Be(ScriptValueType.Number);
        _ = actions[0].ScriptValue.Should().Be("0");

        _ = actions[1].Type.Should().Be(EditorActionType.ForBlockStart);
        _ = actions[1].ForVariableName.Should().Be("i");
        _ = actions[1].ForStartType.Should().Be(ScriptNumericSourceType.Number);
        _ = actions[1].ForStartValue.Should().Be("1");
        _ = actions[1].ForEndType.Should().Be(ScriptNumericSourceType.Number);
        _ = actions[1].ForEndValue.Should().Be("10");

        _ = actions[2].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[2].UseCurrentPosition.Should().BeTrue();

        _ = actions[3].Type.Should().Be(EditorActionType.BlockEnd);

        _ = actions[4].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = actions[4].ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = actions[4].ScriptNumericValue.Should().Be("n");

        _ = actions[5].Type.Should().Be(EditorActionType.KeyPress);
        _ = actions[5].KeyCode.Should().Be(30);

        _ = actions[6].Type.Should().Be(EditorActionType.BlockEnd);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainNamedKeyDownUp_RestoresKeyActions()
    {
        // Arrange
        _ = _keyCodeMapper.GetKeyCode("ctrl").Returns(29);
        _ = _keyCodeMapper.GetKeyName(29).Returns("Ctrl");
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "key down ctrl",
                "key up ctrl",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(2);
        _ = actions[0].Type.Should().Be(EditorActionType.KeyDown);
        _ = actions[0].KeyCode.Should().Be(29);
        _ = actions[0].KeyName.Should().Be("Ctrl");
        _ = actions[1].Type.Should().Be(EditorActionType.KeyUp);
        _ = actions[1].KeyCode.Should().Be(29);
        _ = actions[1].KeyName.Should().Be("Ctrl");
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepContainsNamedSingleTap_RestoresKeyPress()
    {
        // Arrange
        _ = _keyCodeMapper.GetKeyCode("enter").Returns(28);
        _ = _keyCodeMapper.GetKeyName(28).Returns("Enter");
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "tap enter",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(1);
        _ = actions[0].Type.Should().Be(EditorActionType.KeyPress);
        _ = actions[0].KeyCode.Should().Be(28);
        _ = actions[0].KeyName.Should().Be("Enter");
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepContainsScrollWithoutCount_RestoresSingleScrollAction()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "scroll up",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(1);
        _ = actions[0].Type.Should().Be(EditorActionType.ScrollVertical);
        _ = actions[0].ScrollAmount.Should().Be(1);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainRandomDelayRange_RestoresDelayAction()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "repeat 2 {",
                "delay random 10..20",
                "click left",
                "}",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(4);
        _ = actions[0].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = actions[1].Type.Should().Be(EditorActionType.Delay);
        _ = actions[1].UseRandomDelay.Should().BeTrue();
        _ = actions[1].RandomDelayMinMs.Should().Be(10);
        _ = actions[1].RandomDelayMaxMs.Should().Be(20);
        _ = actions[2].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[3].Type.Should().Be(EditorActionType.BlockEnd);
    }

    [Fact]
    public void FromMacroSequence_WhenConditionStepUsesEscapedDollar_RestoresTextLiterals()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "if $$foo == $$bar {",
                "click left",
                "}",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(3);
        _ = actions[0].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[0].ScriptLeftOperandType.Should().Be(ScriptOperandType.Text);
        _ = actions[0].ScriptLeftOperand.Should().Be("$foo");
        _ = actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Text);
        _ = actions[0].ScriptRightOperand.Should().Be("$bar");
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsUseMoveAliasAbsolute_RestoresStructuredMoveAndClick()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "move absolute 200 300",
                "click l",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(2);
        _ = actions[0].Type.Should().Be(EditorActionType.MouseMove);
        _ = actions[0].IsAbsolute.Should().BeTrue();
        _ = actions[0].X.Should().Be(200);
        _ = actions[0].Y.Should().Be(300);
        _ = actions[1].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[1].IsAbsolute.Should().BeTrue();
        _ = actions[1].X.Should().Be(200);
        _ = actions[1].Y.Should().Be(300);
        _ = actions[1].Button.Should().Be(MacroMouseButton.Left);
        _ = actions[1].UseCurrentPosition.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenPixelSearchFeedsVariableMove_RestoresStructuredMouseActions()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "pixelsearch 0 0 100 100 142C2D btn_found btn_x btn_y tolerance 5",
                "if $btn_found == true {",
                "move abs $btn_x $btn_y",
                "click left",
                "}",
            },
        };

        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);
        var saved = _converter.ToMacroSequence(result.Actions, "Variable Move", isAbsolute: true);

        _ = result.Warnings.Should().BeEmpty();
        _ = result.Actions.Should().NotContain(action => action.Type == EditorActionType.RawScriptStep);
        _ = result.Actions.Should().HaveCount(5);
        _ = result.Actions[2].Type.Should().Be(EditorActionType.MouseMove);
        _ = result.Actions[2].CoordinateXToken.Should().Be("$btn_x");
        _ = result.Actions[2].CoordinateYToken.Should().Be("$btn_y");
        _ = result.Actions[3].Type.Should().Be(EditorActionType.MouseClick);
        _ = result.Actions[3].CoordinateXToken.Should().Be("$btn_x");
        _ = result.Actions[3].CoordinateYToken.Should().Be("$btn_y");
        _ = saved.ScriptSteps.Should().ContainSingle(step => step == "move abs $btn_x $btn_y");
        _ = saved.ScriptSteps.Should().Contain("click left");
        _ = saved.Events.Should().BeEmpty();
    }

    [Fact]
    public void ToMacroEvents_WhenMouseCoordinatesUseVariables_RequiresScriptBackedSequence()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.MouseMove,
            CoordinateXToken = "$x",
            CoordinateYToken = "$y",
        };

        var act = () => _converter.ToMacroEvents(action);

        _ = act.Should().Throw<InvalidOperationException>()
            .WithMessage("*script-backed sequence conversion*");
    }

    [Fact]
    public void FromMacroSequence_WhenScriptContainsExplicitMoveClickPairs_RoundTripsMoveEvents()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "move abs 10 10",
                "click left",
                "move abs 20 20",
                "click left",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);
        var saved = _converter.ToMacroSequence(actions, "RoundTrip Move Click Pairs", isAbsolute: true);

        // Assert
        _ = actions.Should().HaveCount(4);
        _ = actions[0].Type.Should().Be(EditorActionType.MouseMove);
        _ = actions[1].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[2].Type.Should().Be(EditorActionType.MouseMove);
        _ = actions[3].Type.Should().Be(EditorActionType.MouseClick);

        _ = saved.Events.Should().HaveCount(4);
        _ = saved.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = saved.Events[0].X.Should().Be(10);
        _ = saved.Events[0].Y.Should().Be(10);
        _ = saved.Events[1].Type.Should().Be(EventType.Click);
        _ = saved.Events[1].X.Should().Be(10);
        _ = saved.Events[1].Y.Should().Be(10);
        _ = saved.Events[2].Type.Should().Be(EventType.MouseMove);
        _ = saved.Events[2].X.Should().Be(20);
        _ = saved.Events[2].Y.Should().Be(20);
        _ = saved.Events[3].Type.Should().Be(EventType.Click);
        _ = saved.Events[3].X.Should().Be(20);
        _ = saved.Events[3].Y.Should().Be(20);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsUseMixedMoveModes_RoundTripsEventCoordinateModes()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "move abs 200 300",
                "click left",
                "move rel-logical 5 -4",
                "click right",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);
        var saved = _converter.ToMacroSequence(actions, "Mixed Script Modes", isAbsolute: false);

        // Assert
        _ = actions.Should().HaveCount(4);
        _ = actions[0].IsAbsolute.Should().BeTrue();
        _ = actions[1].IsAbsolute.Should().BeTrue();
        _ = actions[1].UseCurrentPosition.Should().BeFalse();
        _ = actions[2].IsAbsolute.Should().BeFalse();
        _ = actions[2].CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
        _ = actions[3].IsAbsolute.Should().BeFalse();
        _ = actions[3].CoordinateSpace.Should().Be(MouseCoordinateSpace.LogicalDesktop);
        _ = actions[3].UseCurrentPosition.Should().BeFalse();

        _ = saved.Events.Should().HaveCount(4);
        _ = saved.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        _ = saved.Events[1].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        _ = saved.Events[2].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
        _ = saved.Events[3].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
    }

    [Fact]
    public void ToMacroSequence_WhenScriptBackedAndMoveImmediatelyPrecedesAbsoluteClick_DoesNotDuplicateMoveStep()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.RepeatBlockStart, Text = "1" },
            new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 200, Y = 300 },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MacroMouseButton.Left,
                IsAbsolute = true,
                X = 200,
                Y = 300,
                UseCurrentPosition = false,
            },
            new EditorAction { Type = EditorActionType.BlockEnd },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Script Backed No Duplicate Move", isAbsolute: true);

        // Assert
        _ = sequence.ScriptSteps.Should().Equal(
            "repeat 1 {",
            "move abs 200 300",
            "click left",
            "}");
        _ = sequence.Events.Should().HaveCount(2);
        _ = sequence.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = sequence.Events[1].Type.Should().Be(EventType.Click);
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainCurrentPositionDownUp_PreservesUseCurrentPosition()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "down left",
                "up left",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);
        var saved = _converter.ToMacroSequence(actions, "DownUpCurrentPosition", isAbsolute: false);

        // Assert
        _ = actions.Should().HaveCount(2);
        _ = actions[0].Type.Should().Be(EditorActionType.MouseDown);
        _ = actions[0].UseCurrentPosition.Should().BeTrue();
        _ = actions[0].IsAbsolute.Should().BeFalse();
        _ = actions[1].Type.Should().Be(EditorActionType.MouseUp);
        _ = actions[1].UseCurrentPosition.Should().BeTrue();
        _ = actions[1].IsAbsolute.Should().BeFalse();

        _ = saved.Events.Should().HaveCount(2);
        _ = saved.Events[0].Type.Should().Be(EventType.ButtonPress);
        _ = saved.Events[0].UseCurrentPosition.Should().BeTrue();
        _ = saved.Events[1].Type.Should().Be(EventType.ButtonRelease);
        _ = saved.Events[1].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainAbsoluteMoveThenCurrentPositionClick_PreservesSeparateCurrentPositionAction()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "move abs 120 240",
                "click current left",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(2);
        _ = actions[0].Type.Should().Be(EditorActionType.MouseMove);
        _ = actions[0].IsAbsolute.Should().BeTrue();
        _ = actions[0].X.Should().Be(120);
        _ = actions[0].Y.Should().Be(240);
        _ = actions[1].Type.Should().Be(EditorActionType.MouseClick);
        _ = actions[1].UseCurrentPosition.Should().BeTrue();
        _ = actions[1].IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void FromMacroSequence_WhenScriptStepsContainBreakAndContinue_RestoresLoopControlActions()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "repeat 1 {",
                "break",
                "}",
                "repeat 1 {",
                "continue",
                "}",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(6);
        _ = actions[0].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = actions[1].Type.Should().Be(EditorActionType.Break);
        _ = actions[2].Type.Should().Be(EditorActionType.BlockEnd);
        _ = actions[3].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = actions[4].Type.Should().Be(EditorActionType.Continue);
        _ = actions[5].Type.Should().Be(EditorActionType.BlockEnd);
    }

    [Fact]
    public void FromMacroSequenceWithDiagnostics_WhenScriptStepIsUnsupported_RestoresRawActionAndWarning()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set i 0",
                "tap ctrl+c",
                "click left",
            },
        };

        // Act
        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        // Assert
        _ = result.RestoredFromScriptSteps.Should().BeTrue();
        _ = result.Warnings.Should().HaveCount(1);
        _ = result.Warnings[0].StepIndex.Should().Be(2);
        _ = result.Warnings[0].Step.Should().Be("tap ctrl+c");
        _ = result.Actions.Should().HaveCount(3);
        _ = result.Actions[1].Type.Should().Be(EditorActionType.RawScriptStep);
        _ = result.Actions[1].Text.Should().Be("tap ctrl+c");
    }

    [Fact]
    public void ToMacroSequence_WhenRawScriptStepPresent_PreservesRawStepAndCompiles()
    {
        // Arrange
        _ = _keyCodeMapper.GetKeyCode("ctrl").Returns(29);
        _ = _keyCodeMapper.GetKeyCode("c").Returns(46);
        _ = _keyCodeMapper.IsModifierKeyCode(29).Returns(returnThis: true);
        _ = _keyCodeMapper.IsModifierKeyCode(46).Returns(returnThis: false);

        var actions = new[]
        {
            new EditorAction
            {
                Type = EditorActionType.RawScriptStep,
                Text = "tap ctrl+c",
            },
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Raw Step", isAbsolute: false);

        // Assert
        _ = sequence.ScriptSteps.Should().Equal("tap ctrl+c");
        _ = sequence.Events.Should().HaveCount(4);
        _ = sequence.Events[0].Type.Should().Be(EventType.KeyPress);
        _ = sequence.Events[3].Type.Should().Be(EventType.KeyRelease);
    }

    [Fact]
    public void FromMacroSequence_WhenConditionContainsComparatorText_ParsesUsingEqualityOperator()
    {
        // Arrange
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "if $mode == a>=b {",
                "click left",
                "}",
            },
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        _ = actions.Should().HaveCount(3);
        _ = actions[0].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[0].ScriptConditionOperator.Should().Be(ScriptConditionOperator.Equals);
        _ = actions[0].ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        _ = actions[0].ScriptLeftOperand.Should().Be("mode");
        _ = actions[0].ScriptRightOperandType.Should().Be(ScriptOperandType.Text);
        _ = actions[0].ScriptRightOperand.Should().Be("a>=b");
    }

    [Theory]
    [InlineData(ShellCommandMode.Shell, "shell \"echo ok\" 1 20 300")]
    [InlineData(ShellCommandMode.ShellCapture, "shell capture \"echo ok\" exitCode stdout _ 1 20 300")]
    [InlineData(ShellCommandMode.ShellInput, "shell input \"hello\" \"echo ok\" 1 20 300")]
    [InlineData(ShellCommandMode.ShellCaptureInput, "shell capture-input \"hello\" \"echo ok\" exitCode stdout _ 1 20 300")]
    public void ToMacroSequence_ForShellCommandModes_SerializesExistingShellSyntax(ShellCommandMode mode, string expectedStep)
    {
        var action = new EditorAction
        {
            Type = EditorActionType.ShellCommand,
            ShellCommandMode = mode,
            ShellCommand = "echo ok",
            ShellStandardInput = "hello",
            ShellExitCodeVariableName = "exitCode",
            ShellStandardOutputVariableName = "stdout",
            ShellStandardErrorVariableName = "_",
            ShellRetries = 1,
            ShellBackoffMs = 20,
            ShellTimeoutMs = 300,
        };

        var sequence = _converter.ToMacroSequence([action], "Shell", isAbsolute: false);

        _ = sequence.ScriptSteps.Should().Equal(expectedStep);
    }

    [Theory]
    [InlineData("shell \"echo ok\"", ShellCommandMode.Shell, "echo ok", "", "exit_code", "stdout", "stderr", 0, 0, 0)]
    [InlineData("shell capture \"echo ok\" exitCode stdout _ 2 50 1000", ShellCommandMode.ShellCapture, "echo ok", "", "exitCode", "stdout", "_", 2, 50, 1000)]
    [InlineData("shell input \"stdin text\" \"cat\" 1", ShellCommandMode.ShellInput, "cat", "stdin text", "exit_code", "stdout", "stderr", 1, 0, 0)]
    [InlineData("shell capture-input \"stdin text\" \"cat\" exitCode stdout stderr 0 0 500", ShellCommandMode.ShellCaptureInput, "cat", "stdin text", "exitCode", "stdout", "stderr", 0, 0, 500)]
    public void FromMacroSequence_ForShellForms_RestoresStructuredShellCommand(
        string step,
        ShellCommandMode expectedMode,
        string expectedCommand,
        string expectedInput,
        string expectedExit,
        string expectedStdout,
        string expectedStderr,
        int expectedRetries,
        int expectedBackoff,
        int expectedTimeout)
    {
        var sequence = new MacroSequence { ScriptSteps = { step } };

        var actions = _converter.FromMacroSequence(sequence);

        var action = actions.Should().ContainSingle().Subject;
        _ = action.Type.Should().Be(EditorActionType.ShellCommand);
        _ = action.ShellCommandMode.Should().Be(expectedMode);
        _ = action.ShellCommand.Should().Be(expectedCommand);
        _ = action.ShellStandardInput.Should().Be(expectedInput);
        _ = action.ShellExitCodeVariableName.Should().Be(expectedExit);
        _ = action.ShellStandardOutputVariableName.Should().Be(expectedStdout);
        _ = action.ShellStandardErrorVariableName.Should().Be(expectedStderr);
        _ = action.ShellRetries.Should().Be(expectedRetries);
        _ = action.ShellBackoffMs.Should().Be(expectedBackoff);
        _ = action.ShellTimeoutMs.Should().Be(expectedTimeout);
    }

    [Fact]
    public void FromMacroSequence_WhenShellLineIsInvalid_RestoresRawScriptStep()
    {
        var sequence = new MacroSequence { ScriptSteps = { "shell capture \"echo ok\" onlyTwo targets" } };

        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = result.Actions.Should().ContainSingle().Which.Type.Should().Be(EditorActionType.RawScriptStep);
        _ = result.Warnings.Should().ContainSingle();
    }

    [Theory]
    [InlineData(WindowCommandMode.Active, "window active title activeTitle")]
    [InlineData(WindowCommandMode.Search, "window search title \"Firefox\" windowAddress")]
    [InlineData(WindowCommandMode.Wait, "window wait title \"Firefox\" 2500 windowAddress")]
    [InlineData(WindowCommandMode.Focus, "window focus title \"Firefox\"")]
    [InlineData(WindowCommandMode.Close, "window close title \"Firefox\"")]
    [InlineData(WindowCommandMode.Move, "window move 100 200")]
    [InlineData(WindowCommandMode.Resize, "window resize 800 600")]
    [InlineData(WindowCommandMode.Center, "window center active")]
    [InlineData(WindowCommandMode.Maximize, "window maximize active")]
    [InlineData(WindowCommandMode.Fullscreen, "window fullscreen active")]
    [InlineData(WindowCommandMode.Floating, "window float active")]
    [InlineData(WindowCommandMode.WorkspaceGet, "window getdesktop workspaceName")]
    [InlineData(WindowCommandMode.WorkspaceSwitch, "window setdesktop \"2\"")]
    [InlineData(WindowCommandMode.WorkspaceMoveActive, "window setdesktopforwindow active \"2\"")]
    [InlineData(WindowCommandMode.WorkspaceMoveWindow, "window setdesktopforwindow address 0x123 \"2\"")]
    public void ToMacroSequence_ForWindowCommandModes_SerializesRunScriptWindowSyntax(WindowCommandMode mode, string expectedStep)
    {
        var sequence = _converter.ToMacroSequence([CreateWindowAction(mode)], "Window", isAbsolute: false);

        _ = sequence.ScriptSteps.Should().Equal(expectedStep);
    }

    [Fact]
    public void FromMacroSequence_ForWindowSearchWithEscapedQuote_RestoresStructuredWindowCommand()
    {
        var sequence = new MacroSequence { ScriptSteps = { "window search title \"Fire\\\"fox\" $addr" } };

        var action = _converter.FromMacroSequence(sequence).Should().ContainSingle().Subject;

        _ = action.Type.Should().Be(EditorActionType.WindowCommand);
        _ = action.WindowCommandMode.Should().Be(WindowCommandMode.Search);
        _ = action.WindowSelectorKind.Should().Be("title");
        _ = action.WindowSelectorValue.Should().Be("Fire\"fox");
        _ = action.WindowOutputVariable.Should().Be("addr");
    }

    [Fact]
    public void FromMacroSequence_WhenWindowLineIsInvalid_RestoresRawScriptStep()
    {
        var sequence = new MacroSequence { ScriptSteps = { "window search title $missingTerm" } };

        var result = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = result.Actions.Should().ContainSingle().Which.Type.Should().Be(EditorActionType.RawScriptStep);
        _ = result.Actions[0].Text.Should().Be("window search title $missingTerm");
        _ = result.Warnings.Should().ContainSingle();
    }
}
