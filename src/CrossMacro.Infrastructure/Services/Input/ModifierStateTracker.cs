using System.Collections.Immutable;

namespace CrossMacro.Infrastructure.Services.Input;

/// <summary>
/// Tracks which of the eight Ctrl/Shift/Alt/Meta keys are held. State is a bitmask and every
/// possible set is materialised at most once, so key events never allocate.
/// </summary>
public sealed class ModifierStateTracker
{
    private static readonly int[] ModifierKeyCodes =
    [
        InputEventCode.KEY_LEFTCTRL,
        InputEventCode.KEY_RIGHTCTRL,
        InputEventCode.KEY_LEFTSHIFT,
        InputEventCode.KEY_RIGHTSHIFT,
        InputEventCode.KEY_LEFTALT,
        InputEventCode.KEY_RIGHTALT,
        InputEventCode.KEY_LEFTMETA,
        InputEventCode.KEY_RIGHTMETA,
    ];

    private static readonly IReadOnlySet<int>?[] Snapshots = new IReadOnlySet<int>?[1 << ModifierKeyCodes.Length];

    private int _pressedMask;

    public IReadOnlySet<int> CurrentModifiers => GetSnapshot(Volatile.Read(ref _pressedMask));

    public bool HasModifiers => Volatile.Read(ref _pressedMask) is not 0;

    public void OnKeyPressed(int keyCode)
    {
        var bit = BitFor(keyCode);
        if (bit is not 0)
        {
            _ = Interlocked.Or(ref _pressedMask, bit);
        }
    }

    public void OnKeyReleased(int keyCode)
    {
        var bit = BitFor(keyCode);
        if (bit is not 0)
        {
            _ = Interlocked.And(ref _pressedMask, ~bit);
        }
    }

    public void Clear() => Volatile.Write(ref _pressedMask, 0);

    private static int BitFor(int keyCode)
    {
        var index = Array.IndexOf(ModifierKeyCodes, keyCode);
        return index < 0 ? 0 : 1 << index;
    }

    private static IReadOnlySet<int> GetSnapshot(int mask)
    {
        var snapshot = Volatile.Read(ref Snapshots[mask]);
        if (snapshot is not null)
        {
            return snapshot;
        }

        var builder = ImmutableHashSet.CreateBuilder<int>();
        for (var index = 0; index < ModifierKeyCodes.Length; index++)
        {
            if ((mask & (1 << index)) is not 0)
            {
                _ = builder.Add(ModifierKeyCodes[index]);
            }
        }

        var created = builder.ToImmutable();
        return Interlocked.CompareExchange(ref Snapshots[mask], created, comparand: null) ?? created;
    }
}
