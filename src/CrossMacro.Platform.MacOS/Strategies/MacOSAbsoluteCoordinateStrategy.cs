using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Strategies;

/// <summary>
/// macOS-specific pure absolute coordinate strategy.
/// Uses CGEventGetLocation to get true absolute coordinates directly from macOS.
/// No delta accumulation, no hybrid approach - 100% pure absolute.
/// </summary>
public class MacOSAbsoluteCoordinateStrategy : ICoordinateStrategy
{
    private int _lastX;
    private int _lastY;

    public Task InitializeAsync(CancellationToken ct)
    {
        // Get initial position from macOS
        var eventRef = CoreGraphics.CGEventCreate(IntPtr.Zero);
        if (eventRef == IntPtr.Zero)
        {
            // CGEventCreate failed - default to (0, 0)
            _lastX = 0;
            _lastY = 0;
            return Task.CompletedTask;
        }
        
        var loc = CoreGraphics.CGEventGetLocation(eventRef);
        CoreFoundation.CFRelease(eventRef);
        
        _lastX = (int)loc.X;
        _lastY = (int)loc.Y;
        
        return Task.CompletedTask;
    }

    public (int X, int Y) ProcessPosition(InputCaptureEventArgs e)
    {
        if (e.Type == InputEventType.Sync)
            return (0, 0);

        if (e.Type != InputEventType.MouseMove)
            return (_lastX, _lastY);

        // macOS sends ABS_X and ABS_Y with absolute positions
        if (e.Code == InputEventCode.ABS_X)
        {
            _lastX = e.Value;
        }
        else if (e.Code == InputEventCode.ABS_Y)
        {
            _lastY = e.Value;
        }

        return (_lastX, _lastY);
    }

    public void Dispose()
    {
    }
}
