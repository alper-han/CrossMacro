using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services.Playback;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Core.Tests.Services.Playback;

public class ButtonStateTrackerTests
{
    private readonly ButtonStateTracker _tracker;

    public ButtonStateTrackerTests()
    {
        _tracker = new ButtonStateTracker();
    }

    [Fact]
    public void Press_ShouldAddButtonToPressed()
    {
        // Arrange
        ushort button = 272; // BTN_LEFT

        // Act
        _tracker.Press(button);

        // Assert
        _tracker.PressedButtons.Should().Contain(button);
        _tracker.IsAnyPressed.Should().BeTrue();
    }

    [Fact]
    public void Release_ShouldRemoveButtonFromPressed()
    {
        // Arrange
        ushort button = 272;
        _tracker.Press(button);

        // Act
        _tracker.Release(button);

        // Assert
        _tracker.PressedButtons.Should().NotContain(button);
    }

    [Fact]
    public void IsAnyPressed_ShouldReturnFalse_WhenNoButtonsPressed()
    {
        _tracker.IsAnyPressed.Should().BeFalse();
    }

    [Fact]
    public void IsAnyPressed_ShouldReturnTrue_WhenButtonsPressed()
    {
        // Arrange
        _tracker.Press(272);

        // Assert
        _tracker.IsAnyPressed.Should().BeTrue();
    }

    [Fact]
    public void Clear_ShouldRemoveAllButtons()
    {
        // Arrange
        _tracker.Press(272);
        _tracker.Press(273);
        _tracker.Press(274);

        // Act
        _tracker.Clear();

        // Assert
        _tracker.PressedButtons.Should().BeEmpty();
        _tracker.IsAnyPressed.Should().BeFalse();
    }

    [Fact]
    public void ReleaseAll_ShouldCallSimulatorForEachPressedButton()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        _tracker.Press(272);
        _tracker.Press(273);

        // Act
        _tracker.ReleaseAll(simulator);

        // Assert
        simulator.Received().MouseButton(272, false);
        simulator.Received().MouseButton(273, false);
        _tracker.IsAnyPressed.Should().BeFalse();
    }

    [Fact]
    public void ReleaseAll_ShouldDoNothing_WhenNoButtonsPressed()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();

        // Act
        _tracker.ReleaseAll(simulator);

        // Assert - only failsafe releases should happen, no tracked buttons
        _tracker.IsAnyPressed.Should().BeFalse();
    }

    [Fact]
    public void RestoreAll_ShouldRepressAllButtons()
    {
        // Arrange
        var simulator = Substitute.For<IInputSimulator>();
        ushort[] buttons = [272, 273];

        // Act
        _tracker.RestoreAll(simulator, buttons);

        // Assert
        simulator.Received().MouseButton(272, true);
        simulator.Received().MouseButton(273, true);
        _tracker.PressedButtons.Should().Contain(272);
        _tracker.PressedButtons.Should().Contain(273);
    }

    [Fact]
    public void Press_ShouldBeIdempotent_ForSameButton()
    {
        // Arrange
        ushort button = 272;

        // Act
        _tracker.Press(button);
        _tracker.Press(button);

        // Assert
        _tracker.PressedButtons.Should().HaveCount(1);
    }
}
