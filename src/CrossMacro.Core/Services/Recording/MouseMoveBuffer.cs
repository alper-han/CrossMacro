using System;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Core.Services.Recording;

public class MouseMoveBuffer
{
    private int _pendingRelX;
    private int _pendingRelY;
    private bool _hasPendingMove;
    
    private int _cachedX;
    private int _cachedY;
    
    public int CachedX => _cachedX;
    
    public int CachedY => _cachedY;
    
    public bool HasPendingMove => _hasPendingMove;
    
    public void Reset()
    {
        _pendingRelX = 0;
        _pendingRelY = 0;
        _hasPendingMove = false;
        _cachedX = 0;
        _cachedY = 0;
    }
    
    public void SetPosition(int x, int y)
    {
        _cachedX = x;
        _cachedY = y;
    }
    
    public void UpdateFromSync(int x, int y)
    {
        _cachedX = x;
        _cachedY = y;
    }
    
    public bool AddRelativeEvent(ushort eventCode, int eventValue)
    {
        if (eventCode == InputEventCode.REL_X)
        {
            _pendingRelX += eventValue;
            _hasPendingMove = true;
            return true;
        }
        else if (eventCode == InputEventCode.REL_Y)
        {
            _pendingRelY += eventValue;
            _hasPendingMove = true;
            return true;
        }
        
        return false;
    }
    
    public MacroEvent? Flush(long timestampMs)
    {
        if (!_hasPendingMove)
            return null;
            
        _cachedX += _pendingRelX;
        _cachedY += _pendingRelY;
        
        var macroEvent = new MacroEvent
        {
            Type = EventType.MouseMove,
            Timestamp = timestampMs,
            X = _cachedX,
            Y = _cachedY
        };
        
        _pendingRelX = 0;
        _pendingRelY = 0;
        _hasPendingMove = false;
        
        return macroEvent;
    }
    
    public (int X, int Y) GetCurrentPosition() => (_cachedX, _cachedY);
}
