using CrossMacro.Infrastructure.Services.Scripting.Nodes;

namespace CrossMacro.Infrastructure.Tests.Services.Scripting;

public sealed class RunScriptInterpreterTests
{
    [Fact]
    public void MoveNext_YieldsOnlyCommandsTheInterpreterDoesNotHandle()
    {
        var run = Run(
            "set n 0",
            "probe a",
            "inc n",
            "repeat 2 {",
            "probe b",
            "}",
            "if $n == 1 {",
            "probe c",
            "}",
            "else {",
            "probe d",
            "}");

        _ = run.Commands.Should().Equal("probe a", "probe b", "probe b", "probe c");
        _ = run.Failure.Should().BeNull();
    }

    [Fact]
    public void MoveNext_WhenBreakIsInsideNestedBranches_LeavesOnlyTheInnermostLoop()
    {
        var run = Run(
            "for i from 1 to 2 {",
            "for j from 1 to 3 {",
            "if $j == 2 {",
            "if 1 == 1 {",
            "break",
            "}",
            "}",
            "probe inner",
            "}",
            "probe outer",
            "}");

        _ = run.Commands.Should().Equal("probe inner", "probe outer", "probe inner", "probe outer");
    }

    [Fact]
    public void MoveNext_WhenContinueIsInsideABranch_SkipsTheRestOfTheIteration()
    {
        var run = Run(
            "for i from 1 to 4 {",
            "if $i == 2 {",
            "continue",
            "}",
            "probe $i",
            "}");

        _ = run.Commands.Should().Equal("probe $i", "probe $i", "probe $i");
        _ = run.Variables["i"].Should().Be("4");
    }

    [Fact]
    public void MoveNext_WhenWhileConditionDependsOnWhatTheCallerDoesWithACommand_ObservesTheChange()
    {
        var nodes = Parse("while $go == yes {", "probe", "}", "probe after");
        var variables = new RunScriptVariables();
        variables.Set("go", "yes");
        var interpreter = new RunScriptInterpreter(nodes, variables, RunScriptDialect.Execution);
        var seen = new List<string>();

        while (interpreter.MoveNext(out var command) is RunScriptInterpreter.Status.Command)
        {
            seen.Add(command!.Source.Step);
            if (seen.Count is 3)
            {
                variables.Set("go", "no");
            }
        }

        _ = seen.Should().Equal("probe", "probe", "probe", "probe after");
    }

    [Fact]
    public void MoveNext_WhenForRangeDescends_CountsDownInclusive()
    {
        var run = Run("for i from 3 to 1 {", "probe $i", "}", "probe done");

        _ = run.Commands.Should().HaveCount(4);
        _ = run.Variables["i"].Should().Be("1");
    }

    [Theory]
    [InlineData(true, "Step 2 (line 20): 'break' can only be used inside repeat/while/for blocks.")]
    [InlineData(false, "'break' can only be used inside repeat/while/for blocks.")]
    public void MoveNext_WhenBreakIsOutsideAnyLoop_FailsInTheDialectsWording(bool expansion, string expected)
    {
        var run = Run(expansion ? RunScriptDialect.Expansion : RunScriptDialect.Execution, "if 1 == 1 {", "break", "}");

        _ = run.Failure.Should().Be(expected);
    }

    [Fact]
    public void MoveNext_WhenLoopsAreNested_SpendsOneIterationBudgetAcrossAllOfThem()
    {
        var run = Run(RunScriptDialect.Execution, "repeat 400 {", "repeat 400 {", "}", "}");

        _ = run.Failure.Should().Be("Runtime loop iteration limit exceeded (100000). Check loop exit condition.");
    }

    [Fact]
    public void MoveNext_WhenExpansionLoopNeverEnds_FailsAtTheHeaderOfTheLoop()
    {
        var run = Run(RunScriptDialect.Expansion, "set n 1", "while $n == 1 {", "inc n 0", "}");

        _ = run.Failure.Should().Be("Step 2 (line 20): loop iteration limit exceeded (10000000). Check loop exit condition.");
    }

    [Theory]
    [InlineData(true, "Step 1 (line 10): repeat count must be >= 0.")]
    [InlineData(false, "Repeat count must be an integer >= 0.")]
    public void MoveNext_WhenRepeatCountIsNegative_UsesEachDialectsHistoricalWording(bool expansion, string expected)
    {
        var run = Run(expansion ? RunScriptDialect.Expansion : RunScriptDialect.Execution, "repeat -2 {", "}");

        _ = run.Failure.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, "Step 1 (line 10): for step cannot be 0.")]
    [InlineData(false, "For step cannot be 0.")]
    public void MoveNext_WhenForStepIsZero_UsesEachDialectsHistoricalWording(bool expansion, string expected)
    {
        var run = Run(expansion ? RunScriptDialect.Expansion : RunScriptDialect.Execution, "for i from 1 to 3 step 0 {", "}");

        _ = run.Failure.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, "Step 2 (line 20): Invalid inc/dec amount variable reference '$1x'.")]
    [InlineData(false, "Invalid variable name '1x'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*")]
    public void MoveNext_WhenUpdateAmountIsAMalformedReference_UsesEachDialectsWording(bool expansion, string expected)
    {
        var run = Run(expansion ? RunScriptDialect.Expansion : RunScriptDialect.Execution, "set n 1", "inc n $1x");

        _ = run.Failure.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, "Step 1 (line 10): Invalid variable reference '$1'.")]
    [InlineData(false, "Invalid variable name '1x'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*")]
    public void MoveNext_WhenSetValueHasAMalformedReference_UsesEachDialectsWording(bool expansion, string expected)
    {
        var run = Run(expansion ? RunScriptDialect.Expansion : RunScriptDialect.Execution, "set y $1x");

        _ = run.Failure.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, "Step 1 (line 10): Invalid variable reference '$-'.")]
    [InlineData(false, "Invalid variable name '-'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*")]
    public void MoveNext_WhenConditionOperandIsAMalformedReference_UsesEachDialectsWording(bool expansion, string expected)
    {
        var run = Run(expansion ? RunScriptDialect.Expansion : RunScriptDialect.Execution, "if $- == 1 {", "}");

        _ = run.Failure.Should().Be(expected);
    }

    [Fact]
    public void MoveNext_WhenTokenMixesTextAndReference_OnlyExecutionSubstitutesIntoIt()
    {
        var expansion = Run(RunScriptDialect.Expansion, "set x 2", "set n 1", "inc n 1$x");
        var execution = Run(RunScriptDialect.Execution, "set x 2", "set n 1", "inc n 1$x", "probe $n");

        _ = expansion.Failure.Should().Be("Step 3 (line 30): Invalid inc/dec amount '1$x'. Expected integer.");
        _ = execution.Variables["n"].Should().Be("13");
    }

    [Fact]
    public void MoveNext_WhenCancelled_ThrowsInsteadOfAdvancing()
    {
        using var cancellation = new CancellationTokenSource();
        var interpreter = new RunScriptInterpreter(Parse("probe"), new RunScriptVariables(), RunScriptDialect.Execution, cancellation.Token);
        cancellation.Cancel();

        var act = () => interpreter.MoveNext(out _);

        _ = act.Should().Throw<OperationCanceledException>();
    }

    private static RunResult Run(params string[] steps) => Run(RunScriptDialect.Expansion, steps);

    private static RunResult Run(RunScriptDialect dialect, params string[] steps)
    {
        var variables = new RunScriptVariables();
        var interpreter = new RunScriptInterpreter(Parse(steps), variables, dialect);
        var commands = new List<string>();
        while (true)
        {
            switch (interpreter.MoveNext(out var command))
            {
                case RunScriptInterpreter.Status.Command:
                    commands.Add(command!.Source.Step);
                    break;
                case RunScriptInterpreter.Status.Completed:
                    return new RunResult(commands, variables, Failure: null);
                default:
                    return new RunResult(commands, variables, interpreter.FailureMessage);
            }
        }
    }

    // Steps are numbered from 1 and carry line numbers 10, 20, ... so a diagnostic names both.
    private static IReadOnlyList<RunScriptNode> Parse(params string[] steps)
    {
        var parsed = RunScriptTreeReader.ParseScriptNodes(
            [.. steps.Select((step, index) => new RunScriptStep(step, SourceLineNumber: (index + 1) * 10, SourceIndex: index + 1))]);
        _ = parsed.Success.Should().BeTrue(parsed.ErrorMessage);
        return parsed.Nodes!;
    }

    private sealed record RunResult(List<string> Commands, RunScriptVariables Variables, string? Failure);
}
