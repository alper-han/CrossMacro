
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Tracks the state of modifier keys (Ctrl, Shift, Alt, etc.)
/// Thread-safe implementation for concurrent access.
/// </summary>
public class ModifierStateTracker(IKeyCodeMapper keyCodeMapper) : IModifierStateTracker
{
    private readonly HashSet<int> _pressedModifiers = new();
    private readonly Lock _lock = new();
    private readonly IKeyCodeMapper _keyCodeMapper = keyCodeMapper;
    public IReadOnlySet<int> CurrentModifiers { get; private set; } = System.Collections.Immutable.ImmutableHashSet<int>.Empty;

    public bool HasModifiers
    {
        get
        {
            using (_lock.EnterScope())
            {
                return _pressedModifiers.Count > 0;
            }
        }
    }

    public void OnKeyPressed(int keyCode)
    {
        if (!_keyCodeMapper.IsModifierKeyCode(keyCode))
        {
            return;
        }

        using (_lock.EnterScope())
        {
            if (_pressedModifiers.Add(keyCode))
            {
                RefreshSnapshot();
            }
        }
    }

    public void OnKeyReleased(int keyCode)
    {
        if (!_keyCodeMapper.IsModifierKeyCode(keyCode))
        {
            return;
        }

        using (_lock.EnterScope())
        {
            if (_pressedModifiers.Remove(keyCode))
            {
                RefreshSnapshot();
            }
        }
    }

    public void Clear()
    {
        using (_lock.EnterScope())
        {
            if (_pressedModifiers.Count > 0)
            {
                _pressedModifiers.Clear();
                RefreshSnapshot();
            }
        }
    }

    private void RefreshSnapshot()
    {
        CurrentModifiers = System.Collections.Immutable.ImmutableHashSet.CreateRange(_pressedModifiers);
    }
}
