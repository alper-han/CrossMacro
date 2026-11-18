
namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public sealed class KeyStateTrackerTests
{
    private readonly KeyStateTracker _tracker;

    public KeyStateTrackerTests()
    {
        _tracker = new KeyStateTracker();
    }

    [Fact]
    public void Press_ShouldAddKeyToPressed()
    {
        const int keyCode = 30; // KEY_A

        _tracker.Press(keyCode);

        _ = _tracker.PressedKeys.Should().Contain(keyCode);
    }

    [Fact]
    public void Release_ShouldRemoveKeyFromPressed()
    {
        const int keyCode = 30;
        _tracker.Press(keyCode);

        _tracker.Release(keyCode);

        _ = _tracker.PressedKeys.Should().NotContain(keyCode);
    }

    [Fact]
    public void Clear_ShouldRemoveAllKeys()
    {
        _tracker.Press(30);
        _tracker.Press(31);
        _tracker.Press(32);

        _tracker.Clear();

        _ = _tracker.PressedKeys.Should().BeEmpty();
    }

    [Fact]
    public void ReleaseAll_ShouldCallSimulatorForEachPressedKey()
    {
        var simulator = Substitute.For<IInputSimulator>();
        _tracker.Press(30);
        _tracker.Press(31);

        _tracker.ReleaseAll(simulator);

        simulator.Received().KeyPress(30, pressed: false);
        simulator.Received().KeyPress(31, pressed: false);
        _ = _tracker.PressedKeys.Should().BeEmpty();
    }

    [Fact]
    public void ReleaseAll_ShouldDoNothing_WhenNoKeysPressed()
    {
        var simulator = Substitute.For<IInputSimulator>();

        _tracker.ReleaseAll(simulator);

        simulator.DidNotReceive().KeyPress(Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public void RestoreAll_ShouldRepressAllKeys()
    {
        var simulator = Substitute.For<IInputSimulator>();
        int[] keys = [30, 31];

        _tracker.RestoreAll(simulator, keys);

        simulator.Received().KeyPress(30, pressed: true);
        simulator.Received().KeyPress(31, pressed: true);
        _ = _tracker.PressedKeys.Should().Contain(30);
        _ = _tracker.PressedKeys.Should().Contain(31);
    }

    [Fact]
    public void Press_ShouldBeIdempotent_ForSameKey()
    {
        const int keyCode = 30;

        _tracker.Press(keyCode);
        _tracker.Press(keyCode);

        _ = _tracker.PressedKeys.Should().HaveCount(1);
    }

    [Fact]
    public void PressedKeys_ShouldReturnSnapshot_NotLiveReference()
    {
        _tracker.Press(30);
        var snapshot = _tracker.PressedKeys;

        _tracker.Press(31);

        // Assert - snapshot should not include 31
        _ = snapshot.Should().NotContain(31);
    }
}
