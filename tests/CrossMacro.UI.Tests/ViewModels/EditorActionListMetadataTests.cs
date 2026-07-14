using CrossMacro.Core.Models;
using CrossMacro.UI.ViewModels;
using FluentAssertions;

namespace CrossMacro.UI.Tests.ViewModels;

public sealed class EditorActionListMetadataTests
{
    [Fact]
    public void IsHidden_UsesOnlyTheActiveEditorFilters()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove };
        var shortWait = new EditorAction { Type = EditorActionType.Delay, DelayMs = 5 };

        EditorActionListMetadata.IsHidden(move, hideMouseMoves: true, hideShortWaits: false).Should().BeTrue();
        EditorActionListMetadata.IsHidden(shortWait, hideMouseMoves: false, hideShortWaits: true).Should().BeTrue();
        EditorActionListMetadata.IsHidden(move, hideMouseMoves: false, hideShortWaits: true).Should().BeFalse();
    }

    [Theory]
    [InlineData(EditorActionType.MouseMove, EditorActionVisualKind.Movement)]
    [InlineData(EditorActionType.KeyPress, EditorActionVisualKind.Keyboard)]
    [InlineData(EditorActionType.Delay, EditorActionVisualKind.Timing)]
    [InlineData(EditorActionType.IfBlockStart, EditorActionVisualKind.ControlFlow)]
    public void GetVisualKind_PreservesActionTaxonomy(EditorActionType actionType, EditorActionVisualKind expected)
    {
        EditorActionListMetadata.GetVisualKind(new EditorAction { Type = actionType }, isNoise: false)
            .Should().Be(expected);
    }

    [Fact]
    public void UpdateDragState_TracksMouseButtonBoundaries()
    {
        var isDragging = false;

        EditorActionListMetadata.UpdateDragState(new EditorAction { Type = EditorActionType.MouseDown }, ref isDragging);
        isDragging.Should().BeTrue();
        EditorActionListMetadata.UpdateDragState(new EditorAction { Type = EditorActionType.MouseUp }, ref isDragging);
        isDragging.Should().BeFalse();
    }
}
