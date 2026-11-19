namespace CrossMacro.Infrastructure.Tests.Services.Scripting;

/// <summary>
/// Pins the user-visible diagnostics of the script pipeline (tree parse, static expansion, event
/// compilation): which step and source line each error is attributed to, and the exact wording.
/// </summary>
public sealed class RunScriptDiagnosticsTests
{
    private readonly RunScriptCompiler _compiler = new(MacroPlayerTestFactory.CreateKeyCodeMapper());

    public static TheoryData<string[], string> InvalidScripts => new()
    {
        { ["click left", "}"], "Step 2 (line 20): unexpected closing brace '}'." },
        { ["else {"], "Step 1 (line 10): unexpected 'else' block." },
        { ["repeat 2 {", "click left"], "Missing closing brace '}' for block." },
        { ["if $x == 1 {", "click left", "}", "else {", "click left"], "Missing closing brace '}' for block." },
        { ["click left", "break"], "Step 2 (line 20): 'break' can only be used inside repeat/while/for blocks." },
        { ["continue"], "Step 1 (line 10): 'continue' can only be used inside repeat/while/for blocks." },
        { ["foo {"], "Step 1 (line 10): unsupported block syntax. Expected one of: repeat <count> {, if <left> <op> <right> {, while <left> <op> <right> {, for <var> from <start> to <end> [step <n>] {" },
        { ["for i 1 to 3 {", "}"], "Step 1 (line 10): Invalid for syntax. Expected: for <var> from <start> to <end> [step <n>] {" },
        { ["click left", "move rel $"], "Step 2 (line 20): Invalid variable reference '$'." },
        { ["set a 1", "move rel $1 0"], "Step 2 (line 20): Invalid variable reference '$1'." },
        { ["set a 1", "move rel $- 0"], "Step 2 (line 20): Invalid variable reference '$-'." },
        { ["set a 1", "move rel $missing 0"], "Step 2 (line 20): Unknown variable '$missing'." },
        { ["set x $y"], "Step 1 (line 10): Unknown variable '$y'." },
        { ["set x"], "Step 1 (line 10): Invalid set syntax. Expected: set <name> <value> or set <name>=<value>." },
        { ["set 1x 3"], "Step 1 (line 10): Invalid variable name '1x'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*" },
        { ["if $missing == 1 {", "}"], "Step 1 (line 10): Unknown variable '$missing'." },
        { ["if $$x ?? 1 {", "}"], "Step 1 (line 10): Unsupported condition operator. Allowed: ==, !=, >, >=, <, <=." },
    };

    [Theory]
    [MemberData(nameof(InvalidScripts))]
    public void Compile_WhenScriptIsInvalid_AttributesTheDiagnosticToTheOffendingStep(string[] steps, string expectedError)
    {
        var result = _compiler.Compile(ToSteps(steps));

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be(expectedError);
    }

    [Fact]
    public void Compile_WhenCommandInsideLoopFailsAfterExpansion_ReportsExpandedPositionWithOriginalLine()
    {
        var result = _compiler.Compile(ToSteps("repeat 2 {", "click left", "bogus", "}"));

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 2 (line 30): unsupported step syntax 'bogus'.");
    }

    [Fact]
    public void Compile_WhenCommandInsideLoopReferencesUnknownVariable_ReportsTheCommandsOwnSource()
    {
        var result = _compiler.Compile(ToSteps("repeat 2 {", "click left", "move rel $missing 0", "}"));

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 3 (line 30): Unknown variable '$missing'.");
    }

    [Fact]
    public void Compile_WhenStepsCarryNoSourceMetadata_FallsBackToStepNumbersOnly()
    {
        var result = _compiler.Compile([new RunScriptStep("click left"), new RunScriptStep("break")]);

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1: 'break' can only be used inside repeat/while/for blocks.");
    }

    [Fact]
    public void Compile_WhenRepeatExceedsTheExpansionBudget_ReportsLoopLimitWithHeaderSource()
    {
        var result = _compiler.Compile(ToSteps("repeat 10000001 {", "}"));

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 1 (line 10): loop iteration limit exceeded (10000000). Check loop exit condition.");
    }

    [Fact]
    public void Compile_WhenRuntimeBackedScriptBreaksOutsideLoop_ReportsUnquotedCommandWithSource()
    {
        var result = _compiler.Compile(ToSteps("pixelcolor 1 2 sampled", "break"));

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 2 (line 20): break can only be used inside repeat/while/for blocks.");
    }

    [Fact]
    public void Compile_WhenRuntimeBackedScriptContainsMalformedCommand_ReportsStepAndLine()
    {
        var result = _compiler.Compile(ToSteps("repeat 2 {", "pixelcolor 1", "}"));

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().StartWith("Step 2 (line 20): Invalid pixelcolor syntax.");
    }

    [Fact]
    public void Compile_WhenRuntimeBackedScriptIsValid_KeepsTheStepTextForRuntimeExecution()
    {
        var result = _compiler.Compile(ToSteps("  pixelcolor 1 2 sampled  ", "if $sampled == FF0000 {", "click left", "}"));

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        _ = result.Sequence!.ScriptSteps.Should().Equal("pixelcolor 1 2 sampled", "if $sampled == FF0000 {", "click left", "}");
        _ = result.Sequence.Events.Should().BeEmpty();
    }

    [Fact]
    public void Compile_WhenForLoopFinishes_LoopVariableKeepsItsLastValue()
    {
        var result = _compiler.Compile(ToSteps("for i from 1 to 3 {", "click left", "}", "move rel $i 0"));

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        var move = result.Sequence!.Events.Single(e => e.Type is EventType.MouseMove);
        _ = move.X.Should().Be(3);
    }

    [Fact]
    public void Compile_WhenVariableNamesDifferOnlyByCase_TreatsThemAsTheSameVariable()
    {
        var result = _compiler.Compile(ToSteps("set Total 4", "move rel $total $TOTAL"));

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        var move = result.Sequence!.Events.Single();
        _ = (move.X, move.Y).Should().Be((4, 4));
    }

    [Fact]
    public void Compile_WhenEveryBranchIsSkipped_ExplainsWhichStepsAreRequired()
    {
        var result = _compiler.Compile(ToSteps("if 1 == 2 {", "click left", "}"));

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Run script did not produce any executable events. Add at least one runtime step (move/click/down/up/scroll/key/tap/type).");
    }

    [Fact]
    public void Compile_WhenScriptOnlyContainsVariableCommands_IsDeferredToRuntime()
    {
        var result = _compiler.Compile(ToSteps("set x 1", "inc x"));

        _ = result.Success.Should().BeTrue(result.ErrorMessage);
        _ = result.Sequence!.ScriptSteps.Should().Equal("set x 1", "inc x");
        _ = result.Sequence.Events.Should().BeEmpty();
    }

    [Fact]
    public void Compile_WhenEmptyStepAppearsAfterExpansion_ReportsItAsEmptyStep()
    {
        var result = _compiler.Compile(ToSteps("set cmd click", "$cmd left", "   "));

        _ = result.Success.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("Step 2 (line 30): step cannot be empty.");
    }

    // Steps are numbered from 1 and carry line numbers 10, 20, 30, ... so the tests can tell step index and source line apart.
    private static List<RunScriptStep> ToSteps(params string[] steps) =>
        [.. steps.Select((step, index) => new RunScriptStep(step, SourceLineNumber: (index + 1) * 10, SourceIndex: index + 1))];
}
