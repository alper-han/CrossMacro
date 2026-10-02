
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class EditorActionListMetadataTests
{
    [Fact]
    public void IsHidden_UsesOnlyTheActiveEditorFilters()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove };
        var shortWait = new EditorAction { Type = EditorActionType.Delay, DelayMs = 5 };

        _ = EditorActionListMetadata.IsHidden(move, hideMouseMoves: true, hideShortWaits: false, isDefinitelyIdle: true).Should().BeTrue();
        _ = EditorActionListMetadata.IsHidden(shortWait, hideMouseMoves: false, hideShortWaits: true, isDefinitelyIdle: true).Should().BeTrue();
        _ = EditorActionListMetadata.IsHidden(move, hideMouseMoves: false, hideShortWaits: true, isDefinitelyIdle: true).Should().BeFalse();
    }

    [Theory]
    [InlineData(EditorActionType.MouseMove, EditorActionVisualKind.Movement)]
    [InlineData(EditorActionType.KeyPress, EditorActionVisualKind.Keyboard)]
    [InlineData(EditorActionType.Delay, EditorActionVisualKind.Timing)]
    [InlineData(EditorActionType.MousePosition, EditorActionVisualKind.Variable)]
    [InlineData(EditorActionType.IfBlockStart, EditorActionVisualKind.ControlFlow)]
    public void GetVisualKind_PreservesActionTaxonomy(EditorActionType actionType, EditorActionVisualKind expected)
    {
        _ = EditorActionListMetadata.GetVisualKind(new EditorAction { Type = actionType }, isNoise: false)
            .Should().Be(expected);
    }

    [Fact]
    public void PointerVisualKind_PreservesExistingNumericToken()
    {
        _ = ((int)EditorActionVisualKind.PointerInput).Should().Be(2);
    }

}
