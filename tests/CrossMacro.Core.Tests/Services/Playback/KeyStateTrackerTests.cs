using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services.Playback;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Core.Tests.Services.Playback;

public class KeyStateTrackerTests
{
    private readonly KeyStateTracker _tracker;

    public KeyStateTrackerTests()
    {
        _tracker = new KeyStateTracker();
    }

    [Fact]
    public void Press_ShouldAddKeyToPressed()
    {
        // Arrange
        int keyCode = 30; // KEY_A

        // Act
        _tracker.Press(keyCode);

        // Assert
        _tracker.PressedKeys.Should().Contain(keyCode);
    }

    [Fact]
    public void Release_ShouldRemoveKeyFromPressed()
    {
        // Arrange
        int keyCode = 30;
        _tracker.Press(keyCode);

        // Act
        _tracker.Release(keyCode);

        // Assert
        _tracker.PressedKeys.Should().NotContain(keyCode);
    }

    [Fact]
    public void Clear_ShouldRemoveAllKeys()
    {
        // Arrange
        _tracker.Press(30);
        _tracker.Press(31);
        _tracker.Press(32);

        // Act
        _tracker.Clear();

        // Assert
        _tracker.PressedKeys.Should().BeEmpty();
    }

    [Fact]
    public void ReleaseAll_ShouldCallSimulatorForEachPressedKey()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _tracker.Press(30);
        _tracker.Press(31);

        // Act
        _tracker.ReleaseAll(simulator);

        // Assert
        simulator.Received().KeyPress(30, false);
        simulator.Received().KeyPress(31, false);
        _tracker.PressedKeys.Should().BeEmpty();
    }

    [Fact]
    public void ReleaseAll_ShouldDoNothing_WhenNoKeysPressed()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();

        // Act
        _tracker.ReleaseAll(simulator);

        // Assert
        simulator.DidNotReceive().KeyPress(Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public void RestoreAll_ShouldRepressAllKeys()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        int[] keys = [30, 31];

        // Act
        _tracker.RestoreAll(simulator, keys);

        // Assert
        simulator.Received().KeyPress(30, true);
        simulator.Received().KeyPress(31, true);
        _tracker.PressedKeys.Should().Contain(30);
        _tracker.PressedKeys.Should().Contain(31);
    }

    [Fact]
    public void Press_ShouldBeIdempotent_ForSameKey()
    {
        // Arrange
        int keyCode = 30;

        // Act
        _tracker.Press(keyCode);
        _tracker.Press(keyCode);

        // Assert
        _tracker.PressedKeys.Should().HaveCount(1);
    }

    [Fact]
    public void PressedKeys_ShouldReturnSnapshot_NotLiveReference()
    {
        // Arrange
        _tracker.Press(30);
        var snapshot = _tracker.PressedKeys;

        // Act
        _tracker.Press(31);

        // Assert - snapshot should not include 31
        snapshot.Should().NotContain(31);
    }
}
