using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Matches input key codes against configured hotkey mappings with debounce support.
/// </summary>
public class HotkeyMatcher : IHotkeyMatcher
{
    private readonly Dictionary<string, DateTime> _lastHotkeyPressTimes = new();
    private readonly Lock _lock = new();
    
    private const int DefaultDebounceMs = 300;
    
    public int DebounceIntervalMs { get; set; } = DefaultDebounceMs;
    
    private TimeSpan DebounceInterval => TimeSpan.FromMilliseconds(DebounceIntervalMs);
    
    public bool TryMatch(int keyCode, IReadOnlySet<int> modifiers, HotkeyMapping mapping, string actionName)
    {
        // Check if the main key matches
        if (mapping.MainKey != keyCode)
            return false;

        // Check if all required modifiers are pressed
        if (!mapping.RequiredModifiers.All(m => modifiers.Contains(m)))
            return false;

        // Check if there are no extra modifiers pressed
        if (modifiers.Except(mapping.RequiredModifiers).Any())
            return false;

        // Check debounce
        var now = DateTime.UtcNow;
        using (_lock.EnterScope())
        {
            if (_lastHotkeyPressTimes.TryGetValue(actionName, out var lastTime))
            {
                if (now - lastTime < DebounceInterval)
                {
                    return false;
                }
            }
            _lastHotkeyPressTimes[actionName] = now;
        }

        return true;
    }
    
    public void ResetDebounce()
    {
        using (_lock.EnterScope())
        {
            _lastHotkeyPressTimes.Clear();
        }
    }
}
