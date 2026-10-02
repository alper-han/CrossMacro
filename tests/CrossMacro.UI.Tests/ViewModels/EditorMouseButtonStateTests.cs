namespace CrossMacro.UI.Tests.ViewModels;

public sealed class EditorMouseButtonStateTests
{
    [Fact]
    public void Update_OnlyEndsDragAfterEachPressedButtonIsReleased()
    {
        var state = default(EditorMouseButtonState);

        state.Update(new EditorAction { Type = EditorActionType.MouseDown, Button = MacroMouseButton.Left });
        state.Update(new EditorAction { Type = EditorActionType.MouseDown, Button = MacroMouseButton.Right });
        state.Update(new EditorAction { Type = EditorActionType.MouseUp, Button = MacroMouseButton.Right });

        state.IsAnyButtonPressed.Should().BeTrue();
        state.Update(new EditorAction { Type = EditorActionType.MouseUp, Button = MacroMouseButton.Left });
        state.IsAnyButtonPressed.Should().BeFalse();
    }

    [Fact]
    public void Update_ClickDoesNotReleaseAnIndependentlyHeldButton()
    {
        var state = default(EditorMouseButtonState);

        state.Update(new EditorAction { Type = EditorActionType.MouseDown, Button = MacroMouseButton.Left });
        state.Update(new EditorAction { Type = EditorActionType.MouseClick, Button = MacroMouseButton.Right });

        state.IsAnyButtonPressed.Should().BeTrue();
    }
}
