
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class KeyCodeMapperTests
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
    [InlineData("modifier", "Ctrl", 29)]
    [InlineData("modifier", "Shift", 42)]
    [InlineData("modifier", "Alt", 56)]
    [InlineData("modifier", "AltGr", 100)]
    [InlineData("modifier", "Super", 125)]
    [InlineData("modifier", "Meta", 125)]
    [InlineData("function-key", "F1", 59)]
    [InlineData("function-key", "F2", 60)]
    [InlineData("function-key", "F9", 67)]
    [InlineData("function-key", "F10", 68)]
    [InlineData("function-key", "F11", 87)]
    [InlineData("function-key", "F12", 88)]
    [InlineData("function-key", "F13", 183)]
    [InlineData("function-key", "F20", 190)]
    [InlineData("numpad", "Numpad=", 117)]
    [InlineData("numpad", "NumpadPlus", 78)]
    [InlineData("special-key", "Space", 57)]
    [InlineData("special-key", "Enter", 28)]
    [InlineData("special-key", "Tab", 15)]
    [InlineData("special-key", "Backspace", 14)]
    [InlineData("special-key", "Escape", 1)]
    [InlineData("special-key", "Esc", 1)]
    [InlineData("mouse-button", "Mouse Left", 272)]
    [InlineData("mouse-button", "Mouse Right", 273)]
    [InlineData("mouse-button", "Mouse Middle", 274)]
    public void GetKeyCode_ShouldReturnCorrectCode_ForBuiltInMappings(string category, string keyName, int expectedCode)
    {
        var result = _mapper.GetKeyCode(keyName);

        _ = result.Should().Be(expectedCode, "because {0} key '{1}' has a built-in mapping", category, keyName);
    }

    [Theory]
    [InlineData("F21")]
    [InlineData("F22")]
    [InlineData("F23")]
    [InlineData("F24")]
    public void GetKeyCode_ShouldRejectFunctionKeysWithoutMacOrdinaryMapping_WhenLayoutDoesNotHandleThem(string keyName)
    {
        _ = _layoutService.GetKeyCode(keyName).Returns(-1);

        var result = _mapper.GetKeyCode(keyName);

        _ = result.Should().Be(-1);
    }

    [Theory]
    [InlineData("Help", 138)]
    [InlineData("Mute", 113)]
    [InlineData("VolumeDown", 114)]
    [InlineData("VolumeUp", 115)]
    [InlineData("BrightnessDown", 224)]
    [InlineData("BrightnessUp", 225)]
    [InlineData("PlayPause", 164)]
    [InlineData("PreviousSong", 165)]
    [InlineData("NextSong", 163)]
    [InlineData("Rewind", 168)]
    [InlineData("FastForward", 208)]
    [InlineData("Yen", 124)]
    [InlineData("NumpadJpComma", 95)]
    public void GetKeyCode_ShouldUseLayoutService_ForMacSupportedSemanticNames(string keyName, int expectedCode)
    {
        _ = _layoutService.GetKeyCode(keyName).Returns(expectedCode);

        var result = _mapper.GetKeyCode(keyName);

        _ = result.Should().Be(expectedCode);
    }

    [Fact]
    public void GetKeyCode_ShouldReturnCorrectCode_ForLetters()
    {
        // Layout service returns -1, so fallback to QWERTY
        _ = _layoutService.GetKeyCode(Arg.Any<string>()).Returns(-1);

        _ = _mapper.GetKeyCode("Q").Should().Be(16);
        _ = _mapper.GetKeyCode("A").Should().Be(30);
        _ = _mapper.GetKeyCode("Z").Should().Be(44);
    }

    [Fact]
    public void GetKeyCode_ShouldReturnCorrectCode_ForDigits()
    {
        // Layout service returns -1, so fallback
        _ = _layoutService.GetKeyCode(Arg.Any<string>()).Returns(-1);

        _ = _mapper.GetKeyCode("1").Should().Be(2);
        _ = _mapper.GetKeyCode("0").Should().Be(11);
        _ = _mapper.GetKeyCode("5").Should().Be(6);
    }

    [Fact]
    public void GetKeyCode_ShouldUseLayoutService_WhenAvailable()
    {
        // Arrange
        _ = _layoutService.GetKeyCode("CustomKey").Returns(999);

        // Act
        var result = _mapper.GetKeyCode("CustomKey");

        // Assert
        _ = result.Should().Be(999);
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
        _ = _mapper.IsModifierKeyCode(keyCode).Should().Be(expected);
    }

    [Theory]
    [InlineData(30)]  // A
    [InlineData(57)]  // Space
    [InlineData(28)]  // Enter
    public void IsModifierKeyCode_ShouldReturnFalse_ForNonModifiers(int keyCode)
    {
        _ = _mapper.IsModifierKeyCode(keyCode).Should().BeFalse();
    }

    #endregion

    #region GetKeyName Tests

    [Fact]
    public void GetKeyName_ShouldDelegateToLayoutService()
    {
        // Arrange
        _ = _layoutService.GetKeyName(30).Returns("A");

        // Act
        var result = _mapper.GetKeyName(30);

        // Assert
        _ = result.Should().Be("A");
        _ = _layoutService.Received(1).GetKeyName(30);
    }

    #endregion
}
