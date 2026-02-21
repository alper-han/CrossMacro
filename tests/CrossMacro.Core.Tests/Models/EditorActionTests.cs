using CrossMacro.Core.Models;
using FluentAssertions;

namespace CrossMacro.Core.Tests.Models;

public class EditorActionTests
{
    [Fact]
    public void Clone_CreatesNewActionWithCopiedFields()
    {
        // Arrange
        var source = new EditorAction
        {
            Type = EditorActionType.MouseMove,
            X = 40,
            Y = 55,
            IsAbsolute = false,
            Button = MouseButton.Right,
            KeyCode = 30,
            KeyName = "A",
            DelayMs = 25,
            ScrollAmount = -2,
            Text = "hello"
        };

        // Act
        var clone = source.Clone();

        // Assert
        clone.Should().NotBeSameAs(source);
        clone.Id.Should().NotBe(source.Id);
        clone.Type.Should().Be(source.Type);
        clone.X.Should().Be(source.X);
        clone.Y.Should().Be(source.Y);
        clone.IsAbsolute.Should().Be(source.IsAbsolute);
        clone.Button.Should().Be(source.Button);
        clone.KeyCode.Should().Be(source.KeyCode);
        clone.KeyName.Should().Be(source.KeyName);
        clone.DelayMs.Should().Be(source.DelayMs);
        clone.ScrollAmount.Should().Be(source.ScrollAmount);
        clone.Text.Should().Be(source.Text);
    }

    [Fact]
    public void DisplayName_ForTextInput_TruncatesLongText()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = "abcdefghijklmnopqrstuvwxyz"
        };

        // Act
        var display = action.DisplayName;

        // Assert
        display.Should().Contain("Type");
        display.Should().Contain("...");
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForInvalidDelayAndScroll()
    {
        // Arrange
        var delay = new EditorAction { Type = EditorActionType.Delay, DelayMs = -1 };
        var scroll = new EditorAction { Type = EditorActionType.ScrollVertical, ScrollAmount = 0 };

        // Assert
        delay.IsValid().Should().BeFalse();
        scroll.IsValid().Should().BeFalse();
    }
}
