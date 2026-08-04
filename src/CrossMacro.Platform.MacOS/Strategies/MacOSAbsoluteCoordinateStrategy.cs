
namespace CrossMacro.Platform.MacOS.Strategies;

/// <summary>
/// macOS-specific pure absolute coordinate strategy.
/// Uses CGEventGetLocation to get true absolute coordinates directly from macOS.
/// No delta accumulation, no hybrid approach - 100% pure absolute.
/// </summary>
public sealed class MacOSAbsoluteCoordinateStrategy : ICoordinateStrategy
{
    private int _lastX;
    private int _lastY;
    private bool _hasPendingMovement;

    public bool ProducesLogicalCoordinates => true;

    public bool ProducesRelativeCoordinates => false;

    public Task InitializeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _hasPendingMovement = false;

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

    public CoordinateSample ProcessPosition(CapturedInputEvent e)
    {
        if (e.Type is InputEventType.Sync)
        {
            if (!_hasPendingMovement)
            {
                return CoordinateSample.None;
            }

            _hasPendingMovement = false;
            return CoordinateSample.Create(_lastX, _lastY);
        }

        if (e.Type is not InputEventType.MouseMove)
        {
            return CoordinateSample.Create(_lastX, _lastY);
        }

        // macOS sends ABS_X and ABS_Y with absolute positions
        if (e.Code == InputEventCode.ABS_X)
        {
            _lastX = e.Value;
            _hasPendingMovement = true;
        }
        else if (e.Code == InputEventCode.ABS_Y)
        {
            _lastY = e.Value;
            _hasPendingMovement = true;
        }

        return CoordinateSample.None;
    }

    public void Dispose() { /* Empty */ }
}
