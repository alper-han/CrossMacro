using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Strategies;

/// <summary>
/// Converts macOS CoreGraphics absolute mouse samples into relative deltas.
/// </summary>
public class MacOSRelativeCoordinateStrategy : IRelativeCoordinateStrategy
{
    private readonly Func<(int X, int Y)?>? _currentPositionProvider;
    private int _lastX;
    private int _lastY;
    private int _pendingX;
    private int _pendingY;
    private bool _hasPendingPosition;
    private bool _hasPendingX;
    private bool _hasPendingY;

    public MacOSRelativeCoordinateStrategy(Func<(int X, int Y)?>? currentPositionProvider = null)
    {
        _currentPositionProvider = currentPositionProvider;
    }

    public Task InitializeAsync(CancellationToken ct)
    {
        var position = _currentPositionProvider?.Invoke() ?? GetCurrentPosition();
        if (position.HasValue)
        {
            _lastX = position.Value.X;
            _lastY = position.Value.Y;
        }
        else
        {
            _lastX = 0;
            _lastY = 0;
        }

        _pendingX = _lastX;
        _pendingY = _lastY;
        _hasPendingPosition = false;
        _hasPendingX = false;
        _hasPendingY = false;

        return Task.CompletedTask;
    }

    public (int X, int Y) ProcessPosition(InputCaptureEventArgs e)
    {
        if (e.Type == InputEventType.MouseMove)
        {
            if (e.Code == InputEventCode.ABS_X)
            {
                _pendingX = e.Value;
                _hasPendingPosition = true;
                _hasPendingX = true;
            }
            else if (e.Code == InputEventCode.ABS_Y)
            {
                _pendingY = e.Value;
                _hasPendingPosition = true;
                _hasPendingY = true;
            }

            return (0, 0);
        }

        if (e.Type == InputEventType.Sync)
        {
            return FlushPendingDelta();
        }

        if (e.Type == InputEventType.MouseButton && _hasPendingX && _hasPendingY)
        {
            return FlushPendingDelta();
        }

        return (0, 0);
    }

    public void Dispose()
    {
    }

    private (int X, int Y) FlushPendingDelta()
    {
        if (!_hasPendingPosition)
        {
            return (0, 0);
        }

        int deltaX = _pendingX - _lastX;
        int deltaY = _pendingY - _lastY;

        _lastX = _pendingX;
        _lastY = _pendingY;
        _hasPendingPosition = false;
        _hasPendingX = false;
        _hasPendingY = false;

        return (deltaX, deltaY);
    }

    private static (int X, int Y)? GetCurrentPosition()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        var eventRef = CoreGraphics.CGEventCreate(IntPtr.Zero);
        if (eventRef == IntPtr.Zero)
        {
            return null;
        }

        var location = CoreGraphics.CGEventGetLocation(eventRef);
        CoreFoundation.CFRelease(eventRef);
        return ((int)location.X, (int)location.Y);
    }
}
