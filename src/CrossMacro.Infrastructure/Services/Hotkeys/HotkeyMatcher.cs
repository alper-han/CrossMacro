
namespace CrossMacro.Infrastructure.Services.Hotkeys;

/// <summary>
/// Matches input key codes against configured hotkey mappings. A mapping that fired is ignored
/// again for <see cref="DebounceInterval"/> so key repeat cannot trigger it twice.
/// </summary>
public sealed class HotkeyMatcher(TimeProvider? timeProvider = null)
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(300);

    private readonly Dictionary<string, long> _lastPressTimestamps = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>True when the key and exactly the required modifiers are down and the action is not debounced.</summary>
    public bool TryMatch(int keyCode, IReadOnlySet<int> modifiers, HotkeyMapping mapping, string actionName)
    {
        ArgumentNullException.ThrowIfNull(modifiers);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(actionName);

        if (mapping.MainKey != keyCode
            || modifiers.Count != mapping.RequiredModifiers.Count
            || !mapping.RequiredModifiers.All(modifiers.Contains))
        {
            return false;
        }

        var now = _timeProvider.GetTimestamp();
        using (_lock.EnterScope())
        {
            if (_lastPressTimestamps.TryGetValue(actionName, out var last)
                && _timeProvider.GetElapsedTime(last, now) < DebounceInterval)
            {
                return false;
            }

            _lastPressTimestamps[actionName] = now;
        }

        return true;
    }
}
