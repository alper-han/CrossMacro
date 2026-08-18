
namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Uses globally delivered XInput2 raw-motion events as notifications, then
/// queries the X server for the authoritative root-window pointer position.
/// </summary>
public class X11AbsoluteCapture : X11CaptureBase
{
    private int _lastX;
    private int _lastY;
    private bool _hasPosition;

    public override string ProviderName => "X11 (Absolute Motion)";

    protected override void OnCaptureStarted()
    {
        if (TryGetPointerPosition(out int rootX, out int rootY))
        {
            _lastX = rootX;
            _lastY = rootY;
            _hasPosition = true;
        }
    }

    protected override void ProcessMotion(XGenericEventCookie cookie)
    {
        _ = cookie;
        if (!TryGetPointerPosition(out int x, out int y))
        {
            return;
        }

        if (_hasPosition && x == _lastX && y == _lastY)
        {
            return;
        }

        _lastX = x;
        _lastY = y;
        _hasPosition = true;
        EmitMotion(x, y);
    }

    protected virtual bool TryGetPointerPosition(out int x, out int y)
    {
        return X11Native.XQueryPointer(
            _display,
            _rootWindow,
            out _,
            out _,
            out x,
            out y,
            out _,
            out _,
            out _);
    }

    private void EmitMotion(int x, int y)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        OnInputReceived(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = x,
            Timestamp = timestamp,
            DeviceName = ProviderName,
        });
        OnInputReceived(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = y,
            Timestamp = timestamp,
            DeviceName = ProviderName,
        });

        var argsSync = new CapturedInputEvent
        {
            Type = InputEventType.Sync,
            Timestamp = timestamp,
            DeviceName = ProviderName,
        };
        OnInputReceived(argsSync);
    }
}
