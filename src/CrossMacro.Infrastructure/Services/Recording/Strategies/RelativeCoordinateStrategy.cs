using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Recording.Strategies;

/// <summary>
/// Relative coordinate strategy that buffers X/Y deltas until a SYNC event.
/// This ensures both axes are recorded together in a single MacroEvent.
/// </summary>
public class RelativeCoordinateStrategy : IRelativeCoordinateStrategy
{
    private int _pendingX;
    private int _pendingY;
    private int _lastX;
    private int _lastY;

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
                if (e.Code == InputEventCode.REL_X)
                {
                    _pendingX += e.Value;
                }
                else if (e.Code == InputEventCode.REL_Y)
                {
                    _pendingY += e.Value;
                }

                return (0, 0);

            case InputEventType.Sync:
                _lastX = _pendingX;
                _lastY = _pendingY;
                _pendingX = 0;
                _pendingY = 0;
                return (_lastX, _lastY);

            case InputEventType.MouseButton:
            case InputEventType.MouseScroll:
            case InputEventType.Key:
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
    }
}
