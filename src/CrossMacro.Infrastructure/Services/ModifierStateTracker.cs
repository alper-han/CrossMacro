using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Tracks the state of modifier keys (Ctrl, Shift, Alt, etc.)
/// Thread-safe implementation for concurrent access.
/// </summary>
public class ModifierStateTracker : IModifierStateTracker
{
    private readonly HashSet<int> _pressedModifiers = new();
    private readonly Lock _lock = new();
    private readonly IKeyCodeMapper _keyCodeMapper;
    
    public ModifierStateTracker(IKeyCodeMapper keyCodeMapper)
    {
        _keyCodeMapper = keyCodeMapper;
    }
    
    public IReadOnlySet<int> CurrentModifiers
    {
        get
        {
            using (_lock.EnterScope())
            {
                // Return a copy to ensure thread safety
                return new HashSet<int>(_pressedModifiers);
            }
        }
    }
    
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
            return;
            
        using (_lock.EnterScope())
        {
            _pressedModifiers.Add(keyCode);
        }
    }
    
    public void OnKeyReleased(int keyCode)
    {
        if (!_keyCodeMapper.IsModifierKeyCode(keyCode))
            return;
            
        using (_lock.EnterScope())
        {
            _pressedModifiers.Remove(keyCode);
        }
    }
    
    public void Clear()
    {
        using (_lock.EnterScope())
        {
            _pressedModifiers.Clear();
        }
    }
}
