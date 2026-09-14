namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class EditorScriptGrammarContractTests
{
    [Theory]
    [InlineData("delay random 1..3", EditorActionType.Delay)]
    [InlineData("delay 125us", EditorActionType.Delay)]
    [InlineData("click current back", EditorActionType.MouseClick)]
    [InlineData("scroll left 3", EditorActionType.ScrollHorizontal)]
    [InlineData("move rel-logical 3 -2", EditorActionType.MouseMove)]
    public void SharedInputSyntax_RestoresStructuredActionsAndRecompiles(string step, EditorActionType expectedType)
    {
        var mapper = Substitute.For<IKeyCodeMapper>();
        var converter = new EditorActionConverter(mapper);
        var sequence = new MacroSequence();
        sequence.ReplaceScriptSteps([step]);

        var restored = converter.FromMacroSequenceWithDiagnostics(sequence);
        Assert.Equal(expectedType, Assert.Single(restored.Actions).Type);
        Assert.Empty(restored.Warnings);
        var compiled = new RunScriptCompiler(mapper).Compile([new RunScriptStep(step)]);
        Assert.True(compiled.Success, compiled.ErrorMessage);
        var roundTrip = converter.ToMacroSequence(restored.Actions, "grammar", isAbsolute: false);
        Assert.NotNull(roundTrip);
    }

    [Theory]
    [InlineData("delay random 5 1")]
    [InlineData("scroll right 0")]
    [InlineData("click unknown")]
    public void InvalidInput_RemainsVisibleForRepair_AndCompilerRejectsIt(string step)
    {
        var mapper = Substitute.For<IKeyCodeMapper>();
        var sequence = new MacroSequence();
        sequence.ReplaceScriptSteps([step]);
        var restored = new EditorActionConverter(mapper).FromMacroSequenceWithDiagnostics(sequence);
        var action = Assert.Single(restored.Actions);
        Assert.Equal(EditorActionType.RawScriptStep, action.Type);
        Assert.Equal(step, action.Text);
        Assert.False(new RunScriptCompiler(mapper).Compile([new RunScriptStep(step)]).Success);
    }
}
