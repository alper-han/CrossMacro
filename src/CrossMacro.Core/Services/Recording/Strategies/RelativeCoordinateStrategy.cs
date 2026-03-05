using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Core.Logging;

namespace CrossMacro.Core.Services.Recording.Strategies;

/// <summary>
/// Relative coordinate strategy that buffers X/Y deltas until a SYNC event.
/// This ensures both axes are recorded together in a single MacroEvent.
/// </summary>
public class RelativeCoordinateStrategy : ICoordinateStrategy
{
    // Pending deltas (accumulating until SYNC)
    private int _pendingX;
    private int _pendingY;
    
    // Last flushed values (returned on non-move events)
    private int _lastX;
    private int _lastY;

    public RelativeCoordinateStrategy()
    {
    }

    public Task InitializeAsync(CancellationToken ct)
    {
        _pendingX = 0;
        _pendingY = 0;
        _lastX = 0;
        _lastY = 0;
        
        return Task.CompletedTask;
    }

    public (int X, int Y) ProcessPosition(InputCaptureEventArgs e)
    {
        switch (e.Type)
        {
            case InputEventType.MouseMove:
                // Accumulate deltas
                if (e.Code == InputEventCode.REL_X)
                {
                    _pendingX += e.Value;
                }
                else if (e.Code == InputEventCode.REL_Y)
                {
                    _pendingY += e.Value;
                }
                // Don't return yet - wait for SYNC
                return (0, 0);

            case InputEventType.Sync:
                // Flush: Return accumulated deltas
                _lastX = _pendingX;
                _lastY = _pendingY;
                _pendingX = 0;
                _pendingY = 0;
                return (_lastX, _lastY);

            case InputEventType.MouseButton:
            case InputEventType.MouseScroll:
            case InputEventType.Key:
                // Flush pending on button/key events too (for timing accuracy)
                if (_pendingX != 0 || _pendingY != 0)
                {
                    _lastX = _pendingX;
                    _lastY = _pendingY;
                    _pendingX = 0;
                    _pendingY = 0;
                    return (_lastX, _lastY);
                }
                return (0, 0);

            default:
                return (_lastX, _lastY);
        }
    }

    public void Dispose()
    {
        // No resources to dispose
    }
}
