using System.Diagnostics.CodeAnalysis;

namespace CrossMacro.Platform.Linux.Clipboard;

/// <summary>
/// Bookkeeping for the data offers a Wayland data device announces. A compositor sends one new offer per
/// selection change (plus one per drag), and the protocol requires the client to destroy every offer that
/// is no longer the current selection, so nothing may stay registered once a newer selection arrived.
/// </summary>
internal sealed class WaylandClipboardOfferTracker<TOffer>
    where TOffer : class
{
    private readonly Dictionary<IntPtr, TOffer> _offers = [];

    /// <summary>The offer carrying the current selection, or null when nothing is selected.</summary>
    public TOffer? Current { get; private set; }

    public int Count => _offers.Count;

    /// <summary>Registers a newly announced offer; returns false when the proxy is already known.</summary>
    public bool TryRegister(IntPtr proxy, Func<IntPtr, TOffer> create, out TOffer offer)
    {
        if (_offers.TryGetValue(proxy, out var existing))
        {
            offer = existing;
            return false;
        }

        offer = create(proxy);
        _offers.Add(proxy, offer);
        return true;
    }

    public bool TryGet(IntPtr proxy, [NotNullWhen(true)] out TOffer? offer) =>
        _offers.TryGetValue(proxy, out offer);

    /// <summary>
    /// Makes <paramref name="proxy"/> the current selection (a null or unknown proxy clears it) and returns every
    /// other tracked offer, which the caller must now destroy. Those offers are no longer tracked.
    /// </summary>
    public List<TOffer> Select(IntPtr proxy)
    {
        _ = _offers.TryGetValue(proxy, out var selected);
        var stale = _offers.Values.Where(offer => !ReferenceEquals(offer, selected)).ToList();

        _offers.Clear();
        if (selected is not null)
        {
            _offers.Add(proxy, selected);
        }

        Current = selected;
        return stale;
    }

    /// <summary>Forgets every offer and returns them so the caller can release their resources.</summary>
    public List<TOffer> Clear()
    {
        var all = new List<TOffer>(_offers.Values);
        _offers.Clear();
        Current = null;
        return all;
    }
}
