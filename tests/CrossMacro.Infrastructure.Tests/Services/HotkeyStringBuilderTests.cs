
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class HotkeyStringBuilderTests
{
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly HotkeyStringBuilder _builder;

    // Linux evdev key codes
    private const int LeftCtrl = 29;
    private const int LeftShift = 42;
    private const int LeftAlt = 56;
    private const int LeftSuper = 125;
    private const int KeyA = 30;

    public HotkeyStringBuilderTests()
    {
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _builder = new HotkeyStringBuilder(_keyCodeMapper);
    }

    [Fact]
    public void Build_ShouldReturnKeyName_WhenNoModifiers()
    {
        // Arrange
        _ = _keyCodeMapper.GetKeyName(KeyA).Returns("A");
        var modifiers = new HashSet<int>();

        // Act
        var result = _builder.Build(KeyA, modifiers);

        // Assert
        _ = result.Should().Be("A");
    }

    [Fact]
    public void Build_ShouldIncludeCtrl_WhenCtrlPressed()
    {
        // Arrange
        _ = _keyCodeMapper.GetKeyName(KeyA).Returns("A");
        var modifiers = new HashSet<int> { LeftCtrl };

        // Act
        var result = _builder.Build(KeyA, modifiers);

        // Assert
        _ = result.Should().Be("Ctrl+A");
    }

    [Fact]
    public void Build_ShouldIncludeAllModifiers_WhenMultiplePressed()
    {
        // Arrange
        _ = _keyCodeMapper.GetKeyName(KeyA).Returns("A");
        var modifiers = new HashSet<int> { LeftCtrl, LeftShift, LeftAlt };

        // Act
        var result = _builder.Build(KeyA, modifiers);

        // Assert
        _ = result.Should().Contain("Ctrl");
        _ = result.Should().Contain("Shift");
        _ = result.Should().Contain("Alt");
        _ = result.Should().EndWith("+A");
    }

    [Fact]
    public void Build_ShouldIncludeSuper_WhenSuperPressed()
    {
        // Arrange
        _ = _keyCodeMapper.GetKeyName(KeyA).Returns("A");
        var modifiers = new HashSet<int> { LeftSuper };

        // Act
        var result = _builder.Build(KeyA, modifiers);

        // Assert
        _ = result.Should().Be("Super+A");
    }

    [Fact]
    public void BuildForMouse_ShouldReturnButtonName_WhenNoModifiers()
    {
        // Arrange
        var modifiers = new HashSet<int>();

        // Act
        var result = _builder.BuildForMouse("Left", modifiers);

        // Assert
        _ = result.Should().Be("Left");
    }

    [Fact]
    public void BuildForMouse_ShouldIncludeModifiers()
    {
        // Arrange
        var modifiers = new HashSet<int> { LeftCtrl, LeftShift };

        // Act
        var result = _builder.BuildForMouse("Middle", modifiers);

        // Assert
        _ = result.Should().Contain("Ctrl");
        _ = result.Should().Contain("Shift");
        _ = result.Should().EndWith("+Middle");
    }

    [Fact]
    public void Build_ShouldMaintainModifierOrder()
    {
        // Arrange
        _ = _keyCodeMapper.GetKeyName(KeyA).Returns("A");
        var modifiers = new HashSet<int> { LeftAlt, LeftShift, LeftCtrl }; // Random order

        // Act
        var result = _builder.Build(KeyA, modifiers);

        // Assert - should be Ctrl+Shift+Alt+A (standard order)
        var parts = result.Split('+');
        _ = parts[0].Should().Be("Ctrl");
        _ = parts[1].Should().Be("Shift");
        _ = parts[2].Should().Be("Alt");
        _ = parts[3].Should().Be("A");
    }

    [Theory]
    [InlineData(InputEventCode.KEY_F9, "F9")]
    [InlineData(InputEventCode.KEY_F10, "F10")]
    [InlineData(InputEventCode.KEY_F11, "F11")]
    [InlineData(InputEventCode.KEY_F13, "F13")]
    [InlineData(InputEventCode.KEY_F20, "F20")]
    [InlineData(InputEventCode.KEY_KPEQUAL, "Numpad=")]
    [InlineData(InputEventCode.KEY_HELP, "Help")]
    [InlineData(InputEventCode.KEY_MUTE, "Mute")]
    [InlineData(InputEventCode.KEY_VOLUMEDOWN, "VolumeDown")]
    [InlineData(InputEventCode.KEY_VOLUMEUP, "VolumeUp")]
    [InlineData(InputEventCode.KEY_BRIGHTNESSDOWN, "BrightnessDown")]
    [InlineData(InputEventCode.KEY_BRIGHTNESSUP, "BrightnessUp")]
    [InlineData(InputEventCode.KEY_PLAYPAUSE, "PlayPause")]
    [InlineData(InputEventCode.KEY_PREVIOUSSONG, "PreviousSong")]
    [InlineData(InputEventCode.KEY_NEXTSONG, "NextSong")]
    [InlineData(InputEventCode.KEY_REWIND, "Rewind")]
    [InlineData(InputEventCode.KEY_FASTFORWARD, "FastForward")]
    [InlineData(InputEventCode.KEY_YEN, "Yen")]
    public void Build_ShouldUseMapperDisplayName_ForRoundTripKeys(int keyCode, string displayName)
    {
        _ = _keyCodeMapper.GetKeyName(keyCode).Returns(displayName);
        var modifiers = new HashSet<int>();

        var result = _builder.Build(keyCode, modifiers);

        _ = result.Should().Be(displayName);
    }
}
