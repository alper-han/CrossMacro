namespace CrossMacro.Editing.Tests;

public sealed class ScriptBlockStructureValidatorTests
{
    private static ScriptBlockStructureValidationResult Validate(params EditorActionType[] types) =>
        ScriptBlockStructureValidator.Validate([.. types.Select(type => new EditorAction { Type = type })]);

    [Fact]
    public void Validate_WithNoActions_IsValid()
    {
        var result = Validate();

        _ = result.IsValid.Should().BeTrue();
        _ = result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithBalancedNestedBlocks_IsValid()
    {
        var result = Validate(
            EditorActionType.RepeatBlockStart,
            EditorActionType.IfBlockStart,
            EditorActionType.Break,
            EditorActionType.BlockEnd,
            EditorActionType.ElseBlockStart,
            EditorActionType.Continue,
            EditorActionType.BlockEnd,
            EditorActionType.BlockEnd);

        _ = result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithUnexpectedBlockEnd_ReportsItsPosition()
    {
        var result = Validate(EditorActionType.Delay, EditorActionType.BlockEnd);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().ContainSingle().Which.Should().Contain("Action 2").And.Contain("unexpected block end");
    }

    [Fact]
    public void Validate_WithUnclosedBlocks_ReportsEveryOpenBlock()
    {
        var result = Validate(EditorActionType.WhileBlockStart, EditorActionType.IfBlockStart);

        _ = result.Errors.Should().HaveCount(2);
        _ = result.Errors.Should().Contain(error => error.Contains("Action 1", StringComparison.Ordinal))
            .And.Contain(error => error.Contains("Action 2", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(EditorActionType.Break)]
    [InlineData(EditorActionType.Continue)]
    public void Validate_LoopControlOutsideLoop_IsRejected(EditorActionType control)
    {
        var result = Validate(control);

        _ = result.Errors.Should().ContainSingle().Which.Should().Contain("can only be used inside");
    }

    [Fact]
    public void Validate_LoopControlInsideIfThatIsInsideLoop_IsAccepted()
    {
        _ = Validate(
            EditorActionType.ForBlockStart,
            EditorActionType.IfBlockStart,
            EditorActionType.Break,
            EditorActionType.BlockEnd,
            EditorActionType.BlockEnd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_LoopControlInsideIfWithoutLoop_IsRejected()
    {
        var result = Validate(
            EditorActionType.IfBlockStart,
            EditorActionType.Continue,
            EditorActionType.BlockEnd);

        _ = result.Errors.Should().ContainSingle().Which.Should().Contain("Action 2");
    }

    [Fact]
    public void Validate_ElseAtStart_IsRejected()
    {
        var result = Validate(EditorActionType.ElseBlockStart, EditorActionType.BlockEnd);

        _ = result.Errors.Should().Contain(error => error.Contains("right after the closing brace", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ElseAfterLoopBlock_IsRejected()
    {
        var result = Validate(
            EditorActionType.WhileBlockStart,
            EditorActionType.BlockEnd,
            EditorActionType.ElseBlockStart,
            EditorActionType.BlockEnd);

        _ = result.Errors.Should().ContainSingle().Which.Should().Contain("only valid after an if block");
    }

    [Fact]
    public void Validate_WithNullActions_Throws()
    {
        var act = () => ScriptBlockStructureValidator.Validate(null!);

        _ = act.Should().Throw<ArgumentNullException>();
    }
}
