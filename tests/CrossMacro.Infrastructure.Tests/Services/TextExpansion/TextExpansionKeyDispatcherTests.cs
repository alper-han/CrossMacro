namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

public sealed class TextExpansionKeyDispatcherTests
{
    [Fact]
    public async Task SendKeyAsync_WhenTaggedKeyboardIsSupported_PropagatesTextExpansionMarker()
    {
        var simulator = new TaggedTestInputSimulator(supportsTaggedKeyboardInput: true);

        await TextExpansionKeyDispatcher.SendKeyAsync(
            simulator,
            InputEventCode.KEY_A,
            timeProvider: new PacedTimeProvider(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                (InputEventCode.KEY_A, true, InputEventMarkers.TextExpansionKeyboardEvent),
                (InputEventCode.KEY_A, false, InputEventMarkers.TextExpansionKeyboardEvent),
            ],
            simulator.TaggedKeyPresses);
        Assert.Empty(simulator.RegularKeyPresses);
    }

    [Fact]
    public async Task SendKeyAsync_WhenTaggedKeyboardIsUnsupported_FallsBackToOrdinaryKeyPress()
    {
        var simulator = new TaggedTestInputSimulator(supportsTaggedKeyboardInput: false);

        await TextExpansionKeyDispatcher.SendKeyAsync(
            simulator,
            InputEventCode.KEY_A,
            timeProvider: new PacedTimeProvider(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                (InputEventCode.KEY_A, true),
                (InputEventCode.KEY_A, false),
            ],
            simulator.RegularKeyPresses);
        Assert.Empty(simulator.TaggedKeyPresses);
    }

    [Fact]
    public async Task SendKeyAsync_WithAllModifiers_HoldsModifiersAroundTheKeyAndReleasesThemInReverse()
    {
        var simulator = new TaggedTestInputSimulator(supportsTaggedKeyboardInput: false);

        await TextExpansionKeyDispatcher.SendKeyAsync(
            simulator,
            InputEventCode.KEY_V,
            shift: true,
            altGr: true,
            ctrl: true,
            meta: true,
            timeProvider: new PacedTimeProvider(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                (InputEventCode.KEY_LEFTCTRL, true),
                (InputEventCode.KEY_LEFTMETA, true),
                (InputEventCode.KEY_LEFTSHIFT, true),
                (InputEventCode.KEY_RIGHTALT, true),
                (InputEventCode.KEY_V, true),
                (InputEventCode.KEY_V, false),
                (InputEventCode.KEY_RIGHTALT, false),
                (InputEventCode.KEY_LEFTSHIFT, false),
                (InputEventCode.KEY_LEFTMETA, false),
                (InputEventCode.KEY_LEFTCTRL, false),
            ],
            simulator.RegularKeyPresses);
    }
}
