using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;

namespace CrossMacro.Infrastructure.Tests.Services;

public class EditorActionConverterTests
{
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly EditorActionConverter _converter;

    public EditorActionConverterTests()
    {
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _converter = new EditorActionConverter(_keyCodeMapper);
    }

    [Fact]
    public void ToMacroEvents_KeyPress_ExpandsToPressAndRelease()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.KeyPress,
            KeyCode = 30,
            DelayMs = 55
        };

        // Act
        var events = _converter.ToMacroEvents(action);

        // Assert
        events.Should().HaveCount(2);
        events[0].Type.Should().Be(EventType.KeyPress);
        events[0].KeyCode.Should().Be(30);
        events[0].DelayMs.Should().Be(55);
        events[1].Type.Should().Be(EventType.KeyRelease);
        events[1].KeyCode.Should().Be(30);
        events[1].DelayMs.Should().Be(10);
    }

    [Fact]
    public void ToMacroSequence_WhenDelayIsTrailing_SetsTrailingDelayMs()
    {
        // Arrange
        var actions = new[]
        {
            new EditorAction { Type = EditorActionType.MouseMove, X = 10, Y = 20, DelayMs = 0 },
            new EditorAction { Type = EditorActionType.Delay, DelayMs = 250 }
        };

        // Act
        var sequence = _converter.ToMacroSequence(actions, "Test", isAbsolute: true);

        // Assert
        sequence.Events.Should().HaveCount(1);
        sequence.Events[0].Type.Should().Be(EventType.MouseMove);
        sequence.TrailingDelayMs.Should().Be(250);
    }

    [Fact]
    public void FromMacroEvent_WhenPressFollowedByRelease_MergesToKeyPressAction()
    {
        // Arrange
        var keyPress = new MacroEvent { Type = EventType.KeyPress, KeyCode = 30, DelayMs = 15 };
        var keyRelease = new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30 };

        // Act
        var action = _converter.FromMacroEvent(keyPress, keyRelease);

        // Assert
        action.Type.Should().Be(EditorActionType.KeyPress);
        action.KeyCode.Should().Be(30);
        action.DelayMs.Should().Be(15);
    }

    [Fact]
    public void FromMacroSequence_WhenConsecutivePrintableKeys_MergesToTextInput()
    {
        // Arrange
        _keyCodeMapper.GetCharacterForKeyCode(30, false).Returns('a');
        _keyCodeMapper.GetCharacterForKeyCode(48, false).Returns('b');

        var sequence = new MacroSequence
        {
            Events =
            [
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 30, DelayMs = 12 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30, DelayMs = 0 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 48, DelayMs = 10 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 48, DelayMs = 0 }
            ]
        };

        // Act
        var actions = _converter.FromMacroSequence(sequence);

        // Assert
        actions.Should().HaveCount(1);
        actions[0].Type.Should().Be(EditorActionType.TextInput);
        actions[0].Text.Should().Be("ab");
        actions[0].DelayMs.Should().Be(12);
    }
}
