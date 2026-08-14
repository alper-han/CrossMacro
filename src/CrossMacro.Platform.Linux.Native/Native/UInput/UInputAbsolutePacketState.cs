namespace CrossMacro.Platform.Linux.Native.UInput;

internal sealed class UInputAbsolutePacketState(int width, int height)
{
    private readonly int _width = width;
    private readonly int _height = height;
    private (int X, int Y)? _current =
        UInputDeviceCoordinatePolicy.SupportsAbsoluteCoordinates(width, height) ? (0, 0) : null;
    private (int X, int Y)? _pending;
    private bool _hasAbsoluteAxis;
    private bool _containsOnlyAbsoluteAxes = true;

    public void Observe(ushort type, ushort code, int value)
    {
        if (_current is not { } current)
        {
            return;
        }

        if (type is UInputNative.EV_ABS && code is UInputNative.ABS_X or UInputNative.ABS_Y)
        {
            var pending = _pending ?? current;
            _pending = code is UInputNative.ABS_X ? (value, pending.Y) : (pending.X, value);
            _hasAbsoluteAxis = true;
            return;
        }

        _containsOnlyAbsoluteAxes = false;
    }

    public UInputAbsoluteMovePlan? CompletePacket()
    {
        UInputAbsoluteMovePlan? plan = null;
        if (_hasAbsoluteAxis && _pending is { } target)
        {
            target = UInputDeviceCoordinatePolicy.ClampAbsoluteCoordinates(target.X, target.Y, _width, _height);
            plan = _containsOnlyAbsoluteAxes
                ? UInputDeviceCoordinatePolicy.CreateAbsoluteMovePlan(_current, target, _width, _height)
                : new UInputAbsoluteMovePlan(target, Reassertion: null);
            _current = target;
        }

        _pending = null;
        _hasAbsoluteAxis = false;
        _containsOnlyAbsoluteAxes = true;
        return plan;
    }
}
