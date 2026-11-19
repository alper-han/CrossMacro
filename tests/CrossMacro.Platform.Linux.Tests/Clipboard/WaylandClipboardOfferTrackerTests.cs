using CrossMacro.Platform.Linux.Clipboard;

namespace CrossMacro.Platform.Linux.Tests.Clipboard;

public sealed class WaylandClipboardOfferTrackerTests
{
    private sealed record Offer(IntPtr Proxy);

    private static IntPtr ProxyAt(int id) => new(id);

    private static Offer Register(WaylandClipboardOfferTracker<Offer> tracker, int id)
    {
        Assert.True(tracker.TryRegister(ProxyAt(id), static proxy => new Offer(proxy), out var offer));
        return offer;
    }

    [Fact]
    public void TryRegister_WhenProxyIsAlreadyTracked_ReturnsTheExistingOfferWithoutCreatingAnother()
    {
        var tracker = new WaylandClipboardOfferTracker<Offer>();
        var first = Register(tracker, 1);
        var created = 0;

        var registered = tracker.TryRegister(ProxyAt(1), proxy => { created++; return new Offer(proxy); }, out var again);

        Assert.False(registered);
        Assert.Same(first, again);
        Assert.Equal(0, created);
        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void Select_MakesTheOfferCurrentAndKeepsItTracked()
    {
        var tracker = new WaylandClipboardOfferTracker<Offer>();
        var offer = Register(tracker, 1);

        var stale = tracker.Select(ProxyAt(1));

        Assert.Empty(stale);
        Assert.Same(offer, tracker.Current);
        Assert.True(tracker.TryGet(ProxyAt(1), out var found));
        Assert.Same(offer, found);
    }

    [Fact]
    public void Select_WhenANewerSelectionArrives_ReturnsThePreviousOfferForDestruction()
    {
        var tracker = new WaylandClipboardOfferTracker<Offer>();
        var previous = Register(tracker, 1);
        _ = tracker.Select(ProxyAt(1));
        var next = Register(tracker, 2);

        var stale = tracker.Select(ProxyAt(2));

        Assert.Equal([previous], stale);
        Assert.Same(next, tracker.Current);
        Assert.Equal(1, tracker.Count);
        Assert.False(tracker.TryGet(ProxyAt(1), out _));
    }

    [Fact]
    public void Select_ReturnsOffersThatWereAnnouncedButNeverSelected()
    {
        var tracker = new WaylandClipboardOfferTracker<Offer>();
        var dragOffer = Register(tracker, 1);
        var selection = Register(tracker, 2);

        var stale = tracker.Select(ProxyAt(2));

        Assert.Equal([dragOffer], stale);
        Assert.Same(selection, tracker.Current);
    }

    [Fact]
    public void Select_WhenTheSelectionIsCleared_ReturnsTheCurrentOfferAndClearsIt()
    {
        var tracker = new WaylandClipboardOfferTracker<Offer>();
        var current = Register(tracker, 1);
        _ = tracker.Select(ProxyAt(1));
        var pending = Register(tracker, 2);

        var stale = tracker.Select(IntPtr.Zero);

        Assert.Equal(2, stale.Count);
        Assert.Contains(current, stale);
        Assert.Contains(pending, stale);
        Assert.Null(tracker.Current);
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public void Select_WhenTheProxyIsUnknown_ClearsTheCurrentOfferAndReturnsTheOthers()
    {
        var tracker = new WaylandClipboardOfferTracker<Offer>();
        var known = Register(tracker, 1);
        _ = tracker.Select(ProxyAt(1));

        var stale = tracker.Select(ProxyAt(99));

        Assert.Equal([known], stale);
        Assert.Null(tracker.Current);
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public void RepeatedCopies_KeepTheTrackedOfferCountBounded()
    {
        var tracker = new WaylandClipboardOfferTracker<Offer>();
        var destroyed = 0;

        for (var copy = 1; copy <= 1_000; copy++)
        {
            _ = Register(tracker, copy);
            destroyed += tracker.Select(ProxyAt(copy)).Count;
            Assert.Equal(1, tracker.Count);
        }

        Assert.Equal(999, destroyed);
    }

    [Fact]
    public void Clear_ReturnsEveryTrackedOfferAndForgetsThem()
    {
        var tracker = new WaylandClipboardOfferTracker<Offer>();
        var current = Register(tracker, 1);
        _ = tracker.Select(ProxyAt(1));
        var pending = Register(tracker, 2);

        var all = tracker.Clear();

        Assert.Equal(2, all.Count);
        Assert.Contains(current, all);
        Assert.Contains(pending, all);
        Assert.Null(tracker.Current);
        Assert.Equal(0, tracker.Count);
    }
}
