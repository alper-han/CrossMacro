
namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public sealed class ButtonStateTrackerTests
{
    private readonly ButtonStateTracker _tracker;

    public ButtonStateTrackerTests()
    {
        _tracker = new ButtonStateTracker();
    }

    [Fact]
    public void Press_ShouldAddButtonToPressed()
    {
        const ushort button = 272; // BTN_LEFT

        _tracker.Press(button);

        _ = _tracker.PressedButtons.Should().Contain(button);
        _ = _tracker.IsAnyPressed.Should().BeTrue();
    }

    [Fact]
    public void Release_ShouldRemoveButtonFromPressed()
    {
        const ushort button = 272;
        _tracker.Press(button);

        _tracker.Release(button);

        _ = _tracker.PressedButtons.Should().NotContain(button);
    }

    [Fact]
    public void IsAnyPressed_ShouldReturnFalse_WhenNoButtonsPressed()
    {
        _ = _tracker.IsAnyPressed.Should().BeFalse();
    }

    [Fact]
    public void IsAnyPressed_ShouldReturnTrue_WhenButtonsPressed()
    {
        _tracker.Press(272);

        _ = _tracker.IsAnyPressed.Should().BeTrue();
    }

    [Fact]
    public void Clear_ShouldRemoveAllButtons()
    {
        _tracker.Press(272);
        _tracker.Press(273);
        _tracker.Press(274);

        _tracker.Clear();

        _ = _tracker.PressedButtons.Should().BeEmpty();
        _ = _tracker.IsAnyPressed.Should().BeFalse();
    }

    [Fact]
    public void ReleaseAll_ShouldCallSimulatorForEachPressedButton()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _tracker.Press(272);
        _tracker.Press(273);

        _tracker.ReleaseAll(simulator);

        simulator.Received().MouseButton(272, pressed: false);
        simulator.Received().MouseButton(273, pressed: false);
        _ = _tracker.IsAnyPressed.Should().BeFalse();
    }

    [Fact]
    public void ReleaseAll_ShouldDoNothing_WhenNoButtonsPressed()
    {
        var simulator = Substitute.For<IInputSimulator>();

        _tracker.ReleaseAll(simulator);

        // Assert - only failsafe releases should happen, no tracked buttons
        _ = _tracker.IsAnyPressed.Should().BeFalse();
    }

    [Fact]
    public void RestoreAll_ShouldRepressAllButtons()
    {
        var simulator = Substitute.For<IInputSimulator>();
        ushort[] buttons = [272, 273];

        _tracker.RestoreAll(simulator, buttons);

        simulator.Received().MouseButton(272, pressed: true);
        simulator.Received().MouseButton(273, pressed: true);
        _ = _tracker.PressedButtons.Should().Contain(272);
        _ = _tracker.PressedButtons.Should().Contain(273);
    }

    [Fact]
    public void Press_ShouldBeIdempotent_ForSameButton()
    {
        const ushort button = 272;

        _tracker.Press(button);
        _tracker.Press(button);

        _ = _tracker.PressedButtons.Should().HaveCount(1);
    }

    [Fact]
    public void PressedButtons_ShouldReturnSnapshot_NotLiveReference()
    {
        _tracker.Press(272);
        var snapshot = _tracker.PressedButtons;

        _tracker.Press(273);

        // Assert - snapshot should not include 273
        _ = snapshot.Should().NotContain(273);
    }
}
