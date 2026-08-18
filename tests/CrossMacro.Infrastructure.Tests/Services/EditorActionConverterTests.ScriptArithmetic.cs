// Round-trip pins for arithmetic expressions in block arguments (repeat/for) and
// condition (if/while) operands. Convention: a numeric value field that Core TryParse
// recognizes as a binary expression stores the canonical Format() string (sigils
// included) and is emitted verbatim; the source-type field mirrors the left operand's
// source. Condition operands classify arithmetic only for numeric comparison
// operators (>, >=, <, <=); == and != never evaluate arithmetic at runtime, so their
// text/boolean/color operands keep the plain per-type contract byte-identical.
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class EditorActionConverterTests
{
    [Fact]
    public void FromMacroSequence_WhenRepeatCountIsExpression_RestoresStructuredAction()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set count 20",
                "repeat $count / 10 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions.Should().HaveCount(4);
        _ = actions[1].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = actions[1].ScriptNumericValue.Should().Be("$count / 10");
        _ = actions[1].ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = actions[1].PreferLegacyScriptText.Should().BeFalse();
    }

    [Fact]
    public void RoundTrip_WhenRepeatCountIsExpression_RebuildsIdenticalScriptLine()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set count 20",
                "repeat $count / 10 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);
        var rebuilt = _converter.ToMacroSequence(actions, "round trip", isAbsolute: false);

        _ = rebuilt.ScriptSteps.Should().Contain("repeat $count / 10 {");
        // count / 10 = 2 iterations.
        _ = rebuilt.Events.Should().HaveCount(2);
    }

    [Fact]
    public void RoundTrip_WhenRepeatCountIsSpacelessExpression_CanonicalizesOnRestore()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "repeat 5+3 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions[0].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = actions[0].ScriptNumericValue.Should().Be("5 + 3");

        var rebuilt = _converter.ToMacroSequence(actions, "canonical", isAbsolute: false);
        _ = rebuilt.ScriptSteps.Should().Contain("repeat 5 + 3 {");
        _ = rebuilt.Events.Should().HaveCount(8);
    }

    [Fact]
    public void RoundTrip_WhenForSegmentsAreExpressions_RebuildsIdenticalScriptLine()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set n 4",
                "for i from 0 to $n + 1 step 2 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions[1].Type.Should().Be(EditorActionType.ForBlockStart);
        _ = actions[1].ForStartValue.Should().Be("0");
        _ = actions[1].ForEndType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = actions[1].ForEndValue.Should().Be("$n + 1");
        _ = actions[1].ForHasStep.Should().BeTrue();
        _ = actions[1].ForStepValue.Should().Be("2");

        var rebuilt = _converter.ToMacroSequence(actions, "for round trip", isAbsolute: false);
        _ = rebuilt.ScriptSteps.Should().Contain("for i from 0 to $n + 1 step 2 {");
        // i = 0, 2, 4: three clicks.
        _ = rebuilt.Events.Should().HaveCount(3);
    }

    [Fact]
    public void RoundTrip_WhenForEverySegmentIsExpression_RebuildsIdenticalScriptLine()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set a 1",
                "set b 2",
                "set s 3",
                "for i from $a + 1 to $b * 2 step $s - 1 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions[3].ForStartValue.Should().Be("$a + 1");
        _ = actions[3].ForEndValue.Should().Be("$b * 2");
        _ = actions[3].ForStepValue.Should().Be("$s - 1");

        var rebuilt = _converter.ToMacroSequence(actions, "for full expression", isAbsolute: false);
        _ = rebuilt.ScriptSteps.Should().Contain("for i from $a + 1 to $b * 2 step $s - 1 {");
        // i = 2, 4: two clicks.
        _ = rebuilt.Events.Should().HaveCount(2);
    }

    [Fact]
    public void FromMacroSequence_WhenRepeatCountIsMalformedExpression_KeepsRawTextFallback()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "repeat $count / {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions[0].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = actions[0].Text.Should().Be("$count /");
        _ = actions[0].PreferLegacyScriptText.Should().BeTrue();
    }

    [Fact]
    public void FromMacroSequence_WhenRepeatCountIsSimple_KeepsExistingFieldContract()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "repeat $n {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions[0].Type.Should().Be(EditorActionType.RepeatBlockStart);
        _ = actions[0].ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = actions[0].ScriptNumericValue.Should().Be("n");
    }

    [Fact]
    public void FromMacroSequence_WhenIfConditionLeftOperandIsExpression_RestoresStructuredAction()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set x 4",
                "if $x + 1 > 5 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions.Should().HaveCount(4);
        _ = actions[1].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[1].ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        _ = actions[1].ScriptLeftOperand.Should().Be("$x + 1");
        _ = actions[1].ScriptConditionOperator.Should().Be(ScriptConditionOperator.GreaterThan);
        _ = actions[1].ScriptRightOperandType.Should().Be(ScriptOperandType.Number);
        _ = actions[1].ScriptRightOperand.Should().Be("5");
        _ = actions[1].PreferLegacyScriptText.Should().BeFalse();
    }

    [Fact]
    public void RoundTrip_WhenIfConditionHasExpressionOperand_RebuildsIdenticalScriptLine()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set x 5",
                "if $x + 1 > 5 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);
        var rebuilt = _converter.ToMacroSequence(actions, "round trip", isAbsolute: false);

        _ = rebuilt.ScriptSteps.Should().Contain("if $x + 1 > 5 {");
        // $x + 1 = 6 > 5: the click executes.
        _ = rebuilt.Events.Should().HaveCount(1);
    }

    [Fact]
    public void RoundTrip_WhenIfConditionHasSpacelessExpression_CanonicalizesOnRestore()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set x 4",
                "if $x+1>5 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions[1].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[1].ScriptLeftOperand.Should().Be("$x + 1");

        var rebuilt = _converter.ToMacroSequence(actions, "canonical", isAbsolute: false);
        _ = rebuilt.ScriptSteps.Should().Contain("if $x + 1 > 5 {");
    }

    [Fact]
    public void RoundTrip_WhenWhileConditionHasExpressionOperand_RebuildsIdenticalScriptLine()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set n 1",
                "while $n - 1 > 0 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions.Should().HaveCount(4);
        _ = actions[1].Type.Should().Be(EditorActionType.WhileBlockStart);
        _ = actions[1].ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        _ = actions[1].ScriptLeftOperand.Should().Be("$n - 1");
        _ = actions[1].ScriptConditionOperator.Should().Be(ScriptConditionOperator.GreaterThan);
        _ = actions[1].ScriptRightOperandType.Should().Be(ScriptOperandType.Number);
        _ = actions[1].ScriptRightOperand.Should().Be("0");

        var rebuilt = _converter.ToMacroSequence(actions, "while round trip", isAbsolute: false);
        _ = rebuilt.ScriptSteps.Should().Contain("while $n - 1 > 0 {");
        // $n - 1 = 0 > 0 is false: the body never executes.
        _ = rebuilt.Events.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_WhenIfConditionHasNumberLeftExpressionOperand_RebuildsIdenticalScriptLine()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set k 1",
                "set max 20",
                "if 10 * $k <= $max {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions.Should().HaveCount(5);
        _ = actions[2].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[2].ScriptLeftOperandType.Should().Be(ScriptOperandType.Number);
        _ = actions[2].ScriptLeftOperand.Should().Be("10 * $k");
        _ = actions[2].ScriptConditionOperator.Should().Be(ScriptConditionOperator.LessThanOrEqual);
        _ = actions[2].ScriptRightOperandType.Should().Be(ScriptOperandType.VariableReference);
        _ = actions[2].ScriptRightOperand.Should().Be("max");

        var rebuilt = _converter.ToMacroSequence(actions, "number left round trip", isAbsolute: false);
        _ = rebuilt.ScriptSteps.Should().Contain("if 10 * $k <= $max {");
        // 10 * 1 = 10 <= 20: the click executes.
        _ = rebuilt.Events.Should().HaveCount(1);
    }

    [Fact]
    public void RoundTrip_WhenConditionHasQuotedTextOperand_KeepsTextOperandContract()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set mode fast",
                "if $mode == \"fast\" {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions[1].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[1].ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        _ = actions[1].ScriptLeftOperand.Should().Be("mode");
        _ = actions[1].ScriptConditionOperator.Should().Be(ScriptConditionOperator.Equals);
        _ = actions[1].ScriptRightOperandType.Should().Be(ScriptOperandType.Text);
        _ = actions[1].ScriptRightOperand.Should().Be("\"fast\"");

        var rebuilt = _converter.ToMacroSequence(actions, "text round trip", isAbsolute: false);
        _ = rebuilt.ScriptSteps.Should().Contain("if $mode == \"fast\" {");
    }

    [Fact]
    public void RoundTrip_WhenConditionHasColorOperand_KeepsColorOperandContract()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set c FF0000",
                "if $c == FF0000 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions[1].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[1].ScriptRightOperandType.Should().Be(ScriptOperandType.Color);
        _ = actions[1].ScriptRightOperand.Should().Be("FF0000");

        var rebuilt = _converter.ToMacroSequence(actions, "color round trip", isAbsolute: false);
        _ = rebuilt.ScriptSteps.Should().Contain("if $c == FF0000 {");
    }

    [Fact]
    public void RoundTrip_WhenConditionHasBooleanOperand_KeepsBooleanOperandContract()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set f true",
                "if $f == true {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions[1].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[1].ScriptRightOperandType.Should().Be(ScriptOperandType.Boolean);
        _ = actions[1].ScriptRightOperand.Should().Be("true");

        var rebuilt = _converter.ToMacroSequence(actions, "bool round trip", isAbsolute: false);
        _ = rebuilt.ScriptSteps.Should().Contain("if $f == true {");
    }

    [Fact]
    public void RoundTrip_WhenConditionHasSimpleOperands_KeepsExistingFieldContract()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "set x 6",
                "if $x > 5 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions[1].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[1].ScriptLeftOperandType.Should().Be(ScriptOperandType.VariableReference);
        _ = actions[1].ScriptLeftOperand.Should().Be("x");
        _ = actions[1].ScriptRightOperandType.Should().Be(ScriptOperandType.Number);
        _ = actions[1].ScriptRightOperand.Should().Be("5");

        var rebuilt = _converter.ToMacroSequence(actions, "simple round trip", isAbsolute: false);
        _ = rebuilt.ScriptSteps.Should().Contain("if $x > 5 {");
        _ = rebuilt.Events.Should().HaveCount(1);
    }

    [Fact]
    public void FromMacroSequence_WhenConditionOperandIsEscapedDollarExpression_RestoresTextLiteral()
    {
        // `$$` is an escaped literal dollar, never an arithmetic operand. (The line itself
        // compares text with `>`, which the compiler rejects, so only the restore is pinned.)
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "if $$x + 1 > 5 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions[0].Type.Should().Be(EditorActionType.IfBlockStart);
        _ = actions[0].ScriptLeftOperandType.Should().Be(ScriptOperandType.Text);
        _ = actions[0].ScriptLeftOperand.Should().Be("$x + 1");
    }

    [Fact]
    public void FromMacroSequence_WhenConditionExpressionIsMalformed_RestoresRawScriptStep()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps =
            {
                "if $x + > 5 {",
                "click left",
                "}",
            },
        };

        var actions = _converter.FromMacroSequence(sequence);

        _ = actions[0].Type.Should().Be(EditorActionType.RawScriptStep);
        _ = actions[0].Text.Should().Be("if $x + > 5 {");
    }
}
