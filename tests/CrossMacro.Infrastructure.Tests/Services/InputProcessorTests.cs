using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services.TextExpansion;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class InputProcessorTests
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
        
        var mouseEvent = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = 0, Value = 0 };

        // Act
        _processor.ProcessEvent(mouseEvent);

        // Assert
        charReceived.Should().BeFalse();
    }

    [Fact]
    public void ProcessEvent_ShouldFireCharacterReceived_WhenLayoutReturnsChar()
    {
        // Arrange
        char? receivedChar = null;
        _processor.CharacterReceived += c => receivedChar = c;
        _layoutService.GetCharFromKeyCode(30, false, false, false, false, false, false).Returns('a');
        
        var keyEvent = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 30, Value = 1 };

        // Act
        _processor.ProcessEvent(keyEvent);

        // Assert
        receivedChar.Should().Be('a');
    }

    [Fact]
    public void ProcessEvent_ShouldFireSpecialKeyReceived_ForBackspace()
    {
        // Arrange
        int? receivedKey = null;
        _processor.SpecialKeyReceived += k => receivedKey = k;
        
        var backspaceEvent = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 14, Value = 1 };

        // Act
        _processor.ProcessEvent(backspaceEvent);

        // Assert
        receivedKey.Should().Be(14);
    }

    [Fact]
    public void ProcessEvent_ShouldFireSpecialKeyReceived_ForEnter()
    {
        // Arrange
        int? receivedKey = null;
        _processor.SpecialKeyReceived += k => receivedKey = k;
        
        var enterEvent = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 28, Value = 1 };

        // Act
        _processor.ProcessEvent(enterEvent);

        // Assert
        receivedKey.Should().Be(28);
    }

    [Fact]
    public void ProcessEvent_ShouldIgnoreKeyRelease()
    {
        // Arrange
        char? receivedChar = null;
        _processor.CharacterReceived += c => receivedChar = c;
        _layoutService.GetCharFromKeyCode(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), 
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns('a');
        
        var keyReleaseEvent = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 30, Value = 0 };

        // Act
        _processor.ProcessEvent(keyReleaseEvent);

        // Assert
        receivedChar.Should().BeNull();
    }

    #endregion

    #region Modifier State Tests

    [Fact]
    public void ProcessEvent_ShouldTrackShiftModifier()
    {
        // Arrange
        var shiftPressEvent = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 42, Value = 1 };

        // Act
        _processor.ProcessEvent(shiftPressEvent);

        // Assert
        _processor.AreModifiersPressed.Should().BeTrue();
    }

    [Fact]
    public void ProcessEvent_ShouldReleaseModifier_WhenReleased()
    {
        // Arrange
        var shiftPress = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 42, Value = 1 };
        var shiftRelease = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 42, Value = 0 };

        // Act
        _processor.ProcessEvent(shiftPress);
        _processor.ProcessEvent(shiftRelease);

        // Assert
        _processor.AreModifiersPressed.Should().BeFalse();
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ShouldClearModifierState()
    {
        // Arrange
        var shiftPress = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 42, Value = 1 };
        _processor.ProcessEvent(shiftPress);

        // Act
        _processor.Reset();

        // Assert
        _processor.AreModifiersPressed.Should().BeFalse();
    }

    #endregion

    #region CapsLock Tests

    [Fact]
    public void ProcessEvent_ShouldToggleCapsLock_OnPress()
    {
        // Arrange - simulate CapsLock press affecting character output
        _layoutService.GetCharFromKeyCode(30, false, false, false, false, false, true).Returns('A');
        _layoutService.GetCharFromKeyCode(30, false, false, false, false, false, false).Returns('a');
        
        char? receivedChar = null;
        _processor.CharacterReceived += c => receivedChar = c;

        // Act - press CapsLock then type 'a'
        var capsLockPress = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 58, Value = 1 };
        _processor.ProcessEvent(capsLockPress);
        
        var keyEvent = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 30, Value = 1 };
        _processor.ProcessEvent(keyEvent);

        // Assert - should get uppercase 'A' due to CapsLock
        receivedChar.Should().Be('A');
    }

    #endregion
}
