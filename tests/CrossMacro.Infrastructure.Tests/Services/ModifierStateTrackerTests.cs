
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ModifierStateTrackerTests
{
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly ModifierStateTracker _tracker;

    // Linux evdev key codes
    private const int LeftCtrl = 29;
    private const int RightShift = 54;
    private const int KeyA = 30;

    public ModifierStateTrackerTests()
    {
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _tracker = new ModifierStateTracker(_keyCodeMapper);
    }

    [Fact]
    public void OnKeyPressed_ShouldAddModifier_WhenIsModifierKey()
    {
        // Arrange
        _ = _keyCodeMapper.IsModifierKeyCode(LeftCtrl).Returns(returnThis: true);

        // Act
        _tracker.OnKeyPressed(LeftCtrl);

        // Assert
        _ = _tracker.CurrentModifiers.Should().Contain(LeftCtrl);
        _ = _tracker.HasModifiers.Should().BeTrue();
    }

    [Fact]
    public void OnKeyPressed_ShouldNotAddKey_WhenNotModifier()
    {
        // Arrange
        _ = _keyCodeMapper.IsModifierKeyCode(KeyA).Returns(returnThis: false);

        // Act
        _tracker.OnKeyPressed(KeyA);

        // Assert
        _ = _tracker.CurrentModifiers.Should().BeEmpty();
        _ = _tracker.HasModifiers.Should().BeFalse();
    }

    [Fact]
    public void OnKeyReleased_ShouldRemoveModifier()
    {
        // Arrange
        _ = _keyCodeMapper.IsModifierKeyCode(LeftCtrl).Returns(returnThis: true);
        _tracker.OnKeyPressed(LeftCtrl);

        // Act
        _tracker.OnKeyReleased(LeftCtrl);

        // Assert
        _ = _tracker.CurrentModifiers.Should().NotContain(LeftCtrl);
        _ = _tracker.HasModifiers.Should().BeFalse();
    }

    [Fact]
    public void Clear_ShouldRemoveAllModifiers()
    {
        // Arrange
        _ = _keyCodeMapper.IsModifierKeyCode(LeftCtrl).Returns(returnThis: true);
        _ = _keyCodeMapper.IsModifierKeyCode(RightShift).Returns(returnThis: true);
        _tracker.OnKeyPressed(LeftCtrl);
        _tracker.OnKeyPressed(RightShift);

        // Act
        _tracker.Clear();

        // Assert
        _ = _tracker.CurrentModifiers.Should().BeEmpty();
        _ = _tracker.HasModifiers.Should().BeFalse();
    }

    [Fact]
    public void CurrentModifiers_ShouldReturnCopy_NotLiveReference()
    {
        // Arrange
        _ = _keyCodeMapper.IsModifierKeyCode(LeftCtrl).Returns(returnThis: true);
        _ = _keyCodeMapper.IsModifierKeyCode(RightShift).Returns(returnThis: true);
        _tracker.OnKeyPressed(LeftCtrl);
        var snapshot = _tracker.CurrentModifiers;

        // Act
        _tracker.OnKeyPressed(RightShift);

        // Assert - snapshot should not include new modifier
        _ = snapshot.Should().NotContain(RightShift);
    }

    [Fact]
    public void HasModifiers_ShouldReturnFalse_WhenEmpty()
    {
        _ = _tracker.HasModifiers.Should().BeFalse();
    }
}
