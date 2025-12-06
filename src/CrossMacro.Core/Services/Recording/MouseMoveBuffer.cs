using System;
using CrossMacro.Core.Models;
using CrossMacro.Native.UInput;

namespace CrossMacro.Core.Services.Recording;

/// <summary>
/// Buffers relative mouse movement events and flushes as absolute move events
/// Single Responsibility: Accumulates relative movement deltas and produces absolute coordinates
/// </summary>
public class MouseMoveBuffer
{
    private int _pendingRelX;
    private int _pendingRelY;
    private bool _hasPendingMove;
    
    private int _cachedX;
    private int _cachedY;
    
    /// <summary>
    /// Current cached absolute X position
    /// </summary>
    public int CachedX => _cachedX;
    
    /// <summary>
    /// Current cached absolute Y position
    /// </summary>
    public int CachedY => _cachedY;
    
    /// <summary>
    /// Whether there are pending movements to flush
    /// </summary>
    public bool HasPendingMove => _hasPendingMove;
    
    /// <summary>
    /// Reset the buffer state
    /// </summary>
    public void Reset()
    {
        _pendingRelX = 0;
        _pendingRelY = 0;
        _hasPendingMove = false;
        _cachedX = 0;
        _cachedY = 0;
    }
    
    /// <summary>
    /// Set the initial cached position
    /// </summary>
    public void SetPosition(int x, int y)
    {
        _cachedX = x;
        _cachedY = y;
    }
    
    /// <summary>
    /// Update cached position from external source (e.g., position sync)
    /// </summary>
    public void UpdateFromSync(int x, int y)
    {
        _cachedX = x;
        _cachedY = y;
    }
    
    /// <summary>
    /// Add a relative movement event
    /// </summary>
    /// <returns>True if this was a movement event that was buffered</returns>
    public bool AddRelativeEvent(ushort eventCode, int eventValue)
    {
        if (eventCode == UInputNative.REL_X)
        {
            _pendingRelX += eventValue;
            _hasPendingMove = true;
            return true;
        }
        else if (eventCode == UInputNative.REL_Y)
        {
            _pendingRelY += eventValue;
            _hasPendingMove = true;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Flush pending movements and create a MacroEvent
    /// </summary>
    /// <param name="timestampMs">Current timestamp</param>
    /// <returns>MacroEvent with absolute coordinates, or null if no pending moves</returns>
    public MacroEvent? Flush(long timestampMs)
    {
        if (!_hasPendingMove)
            return null;
            
        // Apply pending deltas to cached position
        _cachedX += _pendingRelX;
        _cachedY += _pendingRelY;
        
        var macroEvent = new MacroEvent
        {
            Type = EventType.MouseMove,
            Timestamp = timestampMs,
            X = _cachedX,
            Y = _cachedY
        };
        
        // Reset buffer
        _pendingRelX = 0;
        _pendingRelY = 0;
        _hasPendingMove = false;
        
        return macroEvent;
    }
    
    /// <summary>
    /// Get current position (for button events that need position)
    /// </summary>
    public (int X, int Y) GetCurrentPosition() => (_cachedX, _cachedY);
}
