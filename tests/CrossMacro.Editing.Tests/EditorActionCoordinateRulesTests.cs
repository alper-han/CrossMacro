using CrossMacro.Editing.Models;
using FluentAssertions;
using Xunit;

namespace CrossMacro.Editing.Tests;

public sealed class EditorActionCoordinateRulesTests
{
    [Theory]
    [InlineData(EditorActionType.MouseMove, true)]
    [InlineData(EditorActionType.MouseClick, true)]
    [InlineData(EditorActionType.MouseDown, true)]
    [InlineData(EditorActionType.MouseUp, true)]
    [InlineData(EditorActionType.Delay, false)]
    [InlineData(EditorActionType.ScrollVertical, false)]
    [InlineData(EditorActionType.PixelColor, false)]
    public void UsesCoordinates_OnlyForMoveAndButtonActions(EditorActionType type, bool expected)
    {
        EditorActionCoordinateRules.UsesCoordinates(type).Should().Be(expected);
    }

    [Theory]
    [InlineData(EditorActionType.MouseClick, true, true)]
    [InlineData(EditorActionType.MouseDown, true, true)]
    [InlineData(EditorActionType.MouseUp, true, true)]
    [InlineData(EditorActionType.MouseClick, false, false)]
    [InlineData(EditorActionType.MouseMove, true, false)]
    [InlineData(EditorActionType.Delay, true, false)]
    public void IsCurrentPositionMouseButton_RequiresButtonActionActingAtTheCursor(EditorActionType type, bool useCurrentPosition, bool expected)
    {
        var action = new EditorAction { Type = type, UseCurrentPosition = useCurrentPosition };

        EditorActionCoordinateRules.IsCurrentPositionMouseButton(action).Should().Be(expected);
    }

    [Fact]
    public void IsCurrentPositionMouseButton_WhenActionIsNull_IsFalse()
    {
        EditorActionCoordinateRules.IsCurrentPositionMouseButton(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(EditorActionType.MouseMove, false, true)]
    [InlineData(EditorActionType.MouseClick, false, true)]
    [InlineData(EditorActionType.MouseClick, true, false)]
    [InlineData(EditorActionType.Delay, false, false)]
    public void HasPositionCoordinates_ExcludesCurrentPositionButtonsAndOtherActions(EditorActionType type, bool useCurrentPosition, bool expected)
    {
        var action = new EditorAction { Type = type, UseCurrentPosition = useCurrentPosition };

        EditorActionCoordinateRules.HasPositionCoordinates(action).Should().Be(expected);
    }

    [Fact]
    public void NormalizeCurrentPositionMouseButton_ClearsCoordinatesOfCurrentPositionButtons()
    {
        var action = new EditorAction { Type = EditorActionType.MouseClick, UseCurrentPosition = true, X = 12, Y = 34, IsAbsolute = true };

        EditorActionCoordinateRules.NormalizeCurrentPositionMouseButton(action);

        action.X.Should().Be(0);
        action.Y.Should().Be(0);
        action.IsAbsolute.Should().BeFalse();
    }

    [Fact]
    public void NormalizeCurrentPositionMouseButtons_LeavesOtherActionsUntouched()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 5, Y = 6, IsAbsolute = true };
        var explicitClick = new EditorAction { Type = EditorActionType.MouseClick, UseCurrentPosition = false, X = 7, Y = 8, IsAbsolute = true };
        var cursorClick = new EditorAction { Type = EditorActionType.MouseDown, UseCurrentPosition = true, X = 9, Y = 10, IsAbsolute = true };

        EditorActionCoordinateRules.NormalizeCurrentPositionMouseButtons([move, explicitClick, cursorClick]);

        (move.X, move.Y, move.IsAbsolute).Should().Be((5, 6, true));
        (explicitClick.X, explicitClick.Y, explicitClick.IsAbsolute).Should().Be((7, 8, true));
        (cursorClick.X, cursorClick.Y, cursorClick.IsAbsolute).Should().Be((0, 0, false));
    }
}
