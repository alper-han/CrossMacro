
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class InputProcessorTests
{
    private readonly IKeyboardLayoutService _layoutService;
    private readonly InputProcessor _processor;

    public InputProcessorTests()
    {
        _layoutService = Substitute.For<IKeyboardLayoutService>();
        _processor = new InputProcessor(_layoutService);
    }

    #region ProcessEvent Tests

    [Fact]
    public void ProcessEvent_ShouldIgnoreNonKeyEvents()
    {
        // Arrange
        var charReceived = false;
        _processor.CharacterReceived += _ => charReceived = true;

        var mouseEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = 0, Value = 0 };

        // Act
        _processor.ProcessEvent(mouseEvent);

        // Assert
        _ = charReceived.Should().BeFalse();
    }

    [Fact]
    public void ProcessEvent_ShouldFireCharacterReceived_WhenLayoutReturnsChar()
    {
        // Arrange
        char? receivedChar = null;
        _processor.CharacterReceived += c => receivedChar = c;
        _ = _layoutService.GetCharFromKeyCode(30, leftShift: false, rightShift: false, rightAlt: false, leftAlt: false, leftCtrl: false, capsLock: false).Returns('a');

        var keyEvent = new CapturedInputEvent { Type = InputEventType.Key, Code = 30, Value = 1 };

        // Act
        _processor.ProcessEvent(keyEvent);

        // Assert
        _ = receivedChar.Should().Be('a');
    }

    [Fact]
    public void ProcessEvent_ShouldFireSpecialKeyReceived_ForBackspace()
    {
        // Arrange
        int? receivedKey = null;
        _processor.SpecialKeyReceived += k => receivedKey = k;

        var backspaceEvent = new CapturedInputEvent { Type = InputEventType.Key, Code = 14, Value = 1 };

        // Act
        _processor.ProcessEvent(backspaceEvent);

        // Assert
        _ = receivedKey.Should().Be(14);
    }

    [Fact]
    public void ProcessEvent_ShouldFireSpecialKeyReceived_ForEnter()
    {
        // Arrange
        int? receivedKey = null;
        _processor.SpecialKeyReceived += k => receivedKey = k;

        var enterEvent = new CapturedInputEvent { Type = InputEventType.Key, Code = 28, Value = 1 };

        // Act
        _processor.ProcessEvent(enterEvent);

        // Assert
        _ = receivedKey.Should().Be(28);
    }

    [Fact]
    public void ProcessEvent_ShouldIgnoreKeyRelease()
    {
        // Arrange
        char? receivedChar = null;
        _processor.CharacterReceived += c => receivedChar = c;
        _ = _layoutService.GetCharFromKeyCode(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns('a');

        var keyReleaseEvent = new CapturedInputEvent { Type = InputEventType.Key, Code = 30, Value = 0 };

        // Act
        _processor.ProcessEvent(keyReleaseEvent);

        // Assert
        _ = receivedChar.Should().BeNull();
    }

    #endregion

    #region Modifier State Tests

    [Fact]
    public void ProcessEvent_ShouldTrackShiftModifier()
    {
        // Arrange
        var shiftPressEvent = new CapturedInputEvent { Type = InputEventType.Key, Code = 42, Value = 1 };

        // Act
        _processor.ProcessEvent(shiftPressEvent);

        // Assert
        _ = _processor.AreModifiersPressed.Should().BeTrue();
    }

    [Fact]
    public void ProcessEvent_ShouldReleaseModifier_WhenReleased()
    {
        // Arrange
        var shiftPress = new CapturedInputEvent { Type = InputEventType.Key, Code = 42, Value = 1 };
        var shiftRelease = new CapturedInputEvent { Type = InputEventType.Key, Code = 42, Value = 0 };

        // Act
        _processor.ProcessEvent(shiftPress);
        _processor.ProcessEvent(shiftRelease);

        // Assert
        _ = _processor.AreModifiersPressed.Should().BeFalse();
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ShouldClearModifierState()
    {
        // Arrange
        var shiftPress = new CapturedInputEvent { Type = InputEventType.Key, Code = 42, Value = 1 };
        _processor.ProcessEvent(shiftPress);

        // Act
        _processor.Reset();

        // Assert
        _ = _processor.AreModifiersPressed.Should().BeFalse();
    }

    #endregion

    #region CapsLock Tests

    [Fact]
    public void ProcessEvent_ShouldToggleCapsLock_OnPress()
    {
        // Arrange - simulate CapsLock press affecting character output
        _ = _layoutService.GetCharFromKeyCode(30, leftShift: false, rightShift: false, rightAlt: false, leftAlt: false, leftCtrl: false, capsLock: true).Returns('A');
        _ = _layoutService.GetCharFromKeyCode(30, leftShift: false, rightShift: false, rightAlt: false, leftAlt: false, leftCtrl: false, capsLock: false).Returns('a');

        char? receivedChar = null;
        _processor.CharacterReceived += c => receivedChar = c;

        // Act - press CapsLock then type 'a'
        var capsLockPress = new CapturedInputEvent { Type = InputEventType.Key, Code = 58, Value = 1 };
        _processor.ProcessEvent(capsLockPress);

        var keyEvent = new CapturedInputEvent { Type = InputEventType.Key, Code = 30, Value = 1 };
        _processor.ProcessEvent(keyEvent);

        // Assert - should get uppercase 'A' due to CapsLock
        _ = receivedChar.Should().Be('A');
    }

    #endregion
}
