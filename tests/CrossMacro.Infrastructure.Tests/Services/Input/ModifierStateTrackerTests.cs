
namespace CrossMacro.Infrastructure.Tests.Services.Input;

public sealed class ModifierStateTrackerTests
{
    private readonly ModifierStateTracker _tracker = new();

    private const int KeyA = 30;

    [Fact]
    public void OnKeyPressed_ShouldAddModifier_WhenIsModifierKey()
    {
        _tracker.OnKeyPressed(InputEventCode.KEY_LEFTCTRL);

        _ = _tracker.CurrentModifiers.Should().BeEquivalentTo([InputEventCode.KEY_LEFTCTRL]);
        _ = _tracker.HasModifiers.Should().BeTrue();
    }

    [Fact]
    public void OnKeyPressed_ShouldNotAddKey_WhenNotModifier()
    {
        _tracker.OnKeyPressed(KeyA);

        _ = _tracker.CurrentModifiers.Should().BeEmpty();
        _ = _tracker.HasModifiers.Should().BeFalse();
    }

    [Fact]
    public void OnKeyReleased_ShouldRemoveOnlyTheReleasedModifier()
    {
        _tracker.OnKeyPressed(InputEventCode.KEY_LEFTCTRL);
        _tracker.OnKeyPressed(InputEventCode.KEY_RIGHTSHIFT);

        _tracker.OnKeyReleased(InputEventCode.KEY_LEFTCTRL);

        _ = _tracker.CurrentModifiers.Should().BeEquivalentTo([InputEventCode.KEY_RIGHTSHIFT]);
    }

    [Fact]
    public void OnKeyReleased_ShouldIgnoreModifiersThatAreNotHeld()
    {
        _tracker.OnKeyReleased(InputEventCode.KEY_LEFTCTRL);

        _ = _tracker.HasModifiers.Should().BeFalse();
    }

    [Fact]
    public void Clear_ShouldRemoveAllModifiers()
    {
        _tracker.OnKeyPressed(InputEventCode.KEY_LEFTCTRL);
        _tracker.OnKeyPressed(InputEventCode.KEY_RIGHTMETA);

        _tracker.Clear();

        _ = _tracker.CurrentModifiers.Should().BeEmpty();
        _ = _tracker.HasModifiers.Should().BeFalse();
    }

    [Fact]
    public void CurrentModifiers_ShouldStayStable_WhenTheStateChangesLater()
    {
        _tracker.OnKeyPressed(InputEventCode.KEY_LEFTCTRL);
        var snapshot = _tracker.CurrentModifiers;

        _tracker.OnKeyPressed(InputEventCode.KEY_RIGHTSHIFT);

        _ = snapshot.Should().BeEquivalentTo([InputEventCode.KEY_LEFTCTRL]);
        _ = _tracker.CurrentModifiers.Should().BeEquivalentTo([InputEventCode.KEY_LEFTCTRL, InputEventCode.KEY_RIGHTSHIFT]);
    }

    [Fact]
    public void CurrentModifiers_ShouldReuseTheSameSnapshotForTheSameKeys()
    {
        _tracker.OnKeyPressed(InputEventCode.KEY_LEFTALT);
        var first = _tracker.CurrentModifiers;
        _tracker.Clear();
        _tracker.OnKeyPressed(InputEventCode.KEY_LEFTALT);

        _ = _tracker.CurrentModifiers.Should().BeSameAs(first);
    }

    [Fact]
    public void HasModifiers_ShouldReturnFalse_WhenEmpty()
    {
        _ = _tracker.HasModifiers.Should().BeFalse();
    }
}
