using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class KeyCodeMapperTests
{
    private readonly IKeyboardLayoutService _layoutService;
    private readonly KeyCodeMapper _mapper;

    public KeyCodeMapperTests()
    {
        _layoutService = Substitute.For<IKeyboardLayoutService>();
        _mapper = new KeyCodeMapper(_layoutService);
    }

    #region GetKeyCode Tests

    [Theory]
    [InlineData("Ctrl", 29)]
    [InlineData("Shift", 42)]
    [InlineData("Alt", 56)]
    [InlineData("AltGr", 100)]
    [InlineData("Super", 125)]
    [InlineData("Meta", 125)]
    public void GetKeyCode_ShouldReturnCorrectCode_ForModifiers(string keyName, int expectedCode)
    {
        var result = _mapper.GetKeyCode(keyName);
        result.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData("F1", 59)]
    [InlineData("F2", 60)]
    [InlineData("F9", 67)]
    [InlineData("F10", 68)]
    [InlineData("F11", 87)]
    [InlineData("F12", 88)]
    [InlineData("F13", 183)]
    [InlineData("F20", 190)]
    public void GetKeyCode_ShouldReturnCorrectCode_ForFunctionKeys(string keyName, int expectedCode)
    {
        var result = _mapper.GetKeyCode(keyName);
        result.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData("F21")]
    [InlineData("F22")]
    [InlineData("F23")]
    [InlineData("F24")]
    public void GetKeyCode_ShouldRejectFunctionKeysWithoutMacOrdinaryMapping_WhenLayoutDoesNotHandleThem(string keyName)
    {
        _layoutService.GetKeyCode(keyName).Returns(-1);

        var result = _mapper.GetKeyCode(keyName);

        result.Should().Be(-1);
    }

    [Theory]
    [InlineData("Numpad=", 117)]
    [InlineData("NumpadPlus", 78)]
    public void GetKeyCode_ShouldReturnCorrectCode_ForMacRoundTripNumpadNames(string keyName, int expectedCode)
    {
        var result = _mapper.GetKeyCode(keyName);

        result.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData("Help", 138)]
    [InlineData("Mute", 113)]
    [InlineData("VolumeDown", 114)]
    [InlineData("VolumeUp", 115)]
    [InlineData("Yen", 124)]
    [InlineData("NumpadJpComma", 95)]
    public void GetKeyCode_ShouldUseLayoutService_ForMacSupportedSemanticNames(string keyName, int expectedCode)
    {
        _layoutService.GetKeyCode(keyName).Returns(expectedCode);

        var result = _mapper.GetKeyCode(keyName);

        result.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData("Space", 57)]
    [InlineData("Enter", 28)]
    [InlineData("Tab", 15)]
    [InlineData("Backspace", 14)]
    [InlineData("Escape", 1)]
    [InlineData("Esc", 1)]
    public void GetKeyCode_ShouldReturnCorrectCode_ForSpecialKeys(string keyName, int expectedCode)
    {
        var result = _mapper.GetKeyCode(keyName);
        result.Should().Be(expectedCode);
    }

    [Fact]
    public void GetKeyCode_ShouldReturnCorrectCode_ForLetters()
    {
        // Layout service returns -1, so fallback to QWERTY
        _layoutService.GetKeyCode(Arg.Any<string>()).Returns(-1);

        _mapper.GetKeyCode("Q").Should().Be(16);
        _mapper.GetKeyCode("A").Should().Be(30);
        _mapper.GetKeyCode("Z").Should().Be(44);
    }

    [Fact]
    public void GetKeyCode_ShouldReturnCorrectCode_ForDigits()
    {
        // Layout service returns -1, so fallback
        _layoutService.GetKeyCode(Arg.Any<string>()).Returns(-1);

        _mapper.GetKeyCode("1").Should().Be(2);
        _mapper.GetKeyCode("0").Should().Be(11);
        _mapper.GetKeyCode("5").Should().Be(6);
    }

    [Fact]
    public void GetKeyCode_ShouldUseLayoutService_WhenAvailable()
    {
        // Arrange
        _layoutService.GetKeyCode("CustomKey").Returns(999);

        // Act
        var result = _mapper.GetKeyCode("CustomKey");

        // Assert
        result.Should().Be(999);
    }

    [Theory]
    [InlineData("Mouse Left", 272)]
    [InlineData("Mouse Right", 273)]
    [InlineData("Mouse Middle", 274)]
    public void GetKeyCode_ShouldReturnCorrectCode_ForMouseButtons(string keyName, int expectedCode)
    {
        var result = _mapper.GetKeyCode(keyName);
        result.Should().Be(expectedCode);
    }

    #endregion

    #region IsModifierKeyCode Tests

    [Theory]
    [InlineData(29, true)]   // Left Ctrl
    [InlineData(97, true)]   // Right Ctrl
    [InlineData(42, true)]   // Left Shift
    [InlineData(54, true)]   // Right Shift
    [InlineData(56, true)]   // Left Alt
    [InlineData(100, true)]  // Right Alt
    [InlineData(125, true)]  // Left Super
    [InlineData(126, true)]  // Right Super
    public void IsModifierKeyCode_ShouldReturnTrue_ForModifiers(int keyCode, bool expected)
    {
        _mapper.IsModifierKeyCode(keyCode).Should().Be(expected);
    }

    [Theory]
    [InlineData(30)]  // A
    [InlineData(57)]  // Space
    [InlineData(28)]  // Enter
    public void IsModifierKeyCode_ShouldReturnFalse_ForNonModifiers(int keyCode)
    {
        _mapper.IsModifierKeyCode(keyCode).Should().BeFalse();
    }

    #endregion

    #region GetKeyName Tests

    [Fact]
    public void GetKeyName_ShouldDelegateToLayoutService()
    {
        // Arrange
        _layoutService.GetKeyName(30).Returns("A");

        // Act
        var result = _mapper.GetKeyName(30);

        // Assert
        result.Should().Be("A");
        _layoutService.Received(1).GetKeyName(30);
    }

    #endregion
}
