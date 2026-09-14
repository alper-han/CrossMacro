namespace CrossMacro.Infrastructure.Tests.Services;

public sealed partial class RunScriptScreenReadRuntimeTests
{
    [Theory]
    [InlineData("'hello' == \"hello\"", true)]
    [InlineData("true == TRUE", true)]
    [InlineData("ff0011 == FF0011", true)]
    [InlineData("000003 == 3", true)]
    [InlineData("1+2 == 3", false)]
    [InlineData("$n + 2 >= 5", true)]
    [InlineData("$n * 2 < 5", false)]
    [InlineData("$$value == '$$value'", true)]
    [InlineData("'Hello' != 'hello'", true)]
    public async Task Conditions_StaticCompilationAndRuntimeChooseTheSameBranch(string condition, bool expected)
    {
        var compiler = new RunScriptCompiler(CreateKeyCodeMapper());
        var compiled = compiler.Compile([
            new RunScriptStep("set n 3"), new RunScriptStep($"if {condition} {{"),
            new RunScriptStep("move rel 1 0"), new RunScriptStep("}"), new RunScriptStep("else {"),
            new RunScriptStep("move rel 2 0"), new RunScriptStep("}"),
        ]);
        Assert.True(compiled.Success, compiled.ErrorMessage);
        var compiledMove = Assert.Single(compiled.Sequence!.Events, item => item.Type is EventType.MouseMove);
        Assert.Equal(expected ? 1 : 2, compiledMove.X);

        var reader = new FakeScreenPixelReader();
        using var player = CreatePlayer(CreatePositionProvider((0, 0)), reader,
            inputSimulatorFactory: () => throw new InvalidOperationException("Pixel-only branch must not acquire input."));
        var macro = new MacroSequence
        {
            ScriptSteps = { "set n 3", $"if {condition} {{", "pixelcolor 1 0 selected", "}", "else {", "pixelcolor 2 0 selected", "}" },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        Assert.Equal(new ScreenPoint(compiledMove.X, 0), Assert.Single(reader.GetPixelPoints));
    }

    [Theory]
    [InlineData("$missing == 1", "Unknown variable '$missing'.")]
    [InlineData("1 / 0 > 0", "Division by zero")]
    [InlineData("'text' < 1", "requires numeric operands")]
    public async Task Conditions_StaticCompilationAndRuntimePreserveErrors(string condition, string expectedError)
    {
        var compiler = new RunScriptCompiler(CreateKeyCodeMapper());
        var compiled = compiler.Compile([new RunScriptStep($"if {condition} {{"), new RunScriptStep("move rel 1 0"), new RunScriptStep("}")]);
        Assert.False(compiled.Success);
        Assert.Contains(expectedError, compiled.ErrorMessage, StringComparison.Ordinal);

        using var player = CreatePlayer(CreatePositionProvider((0, 0)), new FakeScreenPixelReader());
        var macro = new MacroSequence { ScriptSteps = { $"if {condition} {{", "pixelcolor 1 0 selected", "}" } };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => player.PlayAsync(macro, cancellationToken: CancellationToken.None));
        Assert.Contains(expectedError, error.Message, StringComparison.Ordinal);
    }
}
