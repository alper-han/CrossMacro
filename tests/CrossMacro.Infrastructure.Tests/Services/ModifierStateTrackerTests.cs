using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class ModifierStateTrackerTests
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
        _keyCodeMapper.IsModifierKeyCode(LeftCtrl).Returns(true);

        // Act
        _tracker.OnKeyPressed(LeftCtrl);

        // Assert
        _tracker.CurrentModifiers.Should().Contain(LeftCtrl);
        _tracker.HasModifiers.Should().BeTrue();
    }

    [Fact]
    public void OnKeyPressed_ShouldNotAddKey_WhenNotModifier()
    {
        // Arrange
        _keyCodeMapper.IsModifierKeyCode(KeyA).Returns(false);

        // Act
        _tracker.OnKeyPressed(KeyA);

        // Assert
        _tracker.CurrentModifiers.Should().BeEmpty();
        _tracker.HasModifiers.Should().BeFalse();
    }

    [Fact]
    public void OnKeyReleased_ShouldRemoveModifier()
    {
        // Arrange
        _keyCodeMapper.IsModifierKeyCode(LeftCtrl).Returns(true);
        _tracker.OnKeyPressed(LeftCtrl);

        // Act
        _tracker.OnKeyReleased(LeftCtrl);

        // Assert
        _tracker.CurrentModifiers.Should().NotContain(LeftCtrl);
        _tracker.HasModifiers.Should().BeFalse();
    }

    [Fact]
    public void Clear_ShouldRemoveAllModifiers()
    {
        // Arrange
        _keyCodeMapper.IsModifierKeyCode(LeftCtrl).Returns(true);
        _keyCodeMapper.IsModifierKeyCode(RightShift).Returns(true);
        _tracker.OnKeyPressed(LeftCtrl);
        _tracker.OnKeyPressed(RightShift);

        // Act
        _tracker.Clear();

        // Assert
        _tracker.CurrentModifiers.Should().BeEmpty();
        _tracker.HasModifiers.Should().BeFalse();
    }

    [Fact]
    public void CurrentModifiers_ShouldReturnCopy_NotLiveReference()
    {
        // Arrange
        _keyCodeMapper.IsModifierKeyCode(LeftCtrl).Returns(true);
        _keyCodeMapper.IsModifierKeyCode(RightShift).Returns(true);
        _tracker.OnKeyPressed(LeftCtrl);
        var snapshot = _tracker.CurrentModifiers;

        // Act
        _tracker.OnKeyPressed(RightShift);

        // Assert - snapshot should not include new modifier
        snapshot.Should().NotContain(RightShift);
    }

    [Fact]
    public void HasModifiers_ShouldReturnFalse_WhenEmpty()
    {
        _tracker.HasModifiers.Should().BeFalse();
    }
}
