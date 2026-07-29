
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class EditorActionListMetadataTests
{
    [Fact]
    public void IsHidden_UsesOnlyTheActiveEditorFilters()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove };
        var shortWait = new EditorAction { Type = EditorActionType.Delay, DelayMs = 5 };

        _ = EditorActionListMetadata.IsHidden(move, hideMouseMoves: true, hideShortWaits: false).Should().BeTrue();
        _ = EditorActionListMetadata.IsHidden(shortWait, hideMouseMoves: false, hideShortWaits: true).Should().BeTrue();
        _ = EditorActionListMetadata.IsHidden(move, hideMouseMoves: false, hideShortWaits: true).Should().BeFalse();
    }

    [Theory]
    [InlineData(EditorActionType.MouseMove, EditorActionVisualKind.Movement)]
    [InlineData(EditorActionType.KeyPress, EditorActionVisualKind.Keyboard)]
    [InlineData(EditorActionType.Delay, EditorActionVisualKind.Timing)]
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

    [Fact]
    public void UpdateDragState_TracksMouseButtonBoundaries()
    {
        var isDragging = false;

        EditorActionListMetadata.UpdateDragState(new EditorAction { Type = EditorActionType.MouseDown }, ref isDragging);
        _ = isDragging.Should().BeTrue();
        EditorActionListMetadata.UpdateDragState(new EditorAction { Type = EditorActionType.MouseUp }, ref isDragging);
        _ = isDragging.Should().BeFalse();
    }
}
