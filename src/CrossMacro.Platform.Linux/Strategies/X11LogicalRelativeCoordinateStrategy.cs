namespace CrossMacro.Platform.Linux.Strategies;

/// <summary>
/// Converts XInput2 root-coordinate motion into logical desktop deltas.
/// </summary>
public sealed class X11LogicalRelativeCoordinateStrategy(
    IMousePositionProvider positionProvider) : IRelativeCoordinateStrategy
{
    private readonly IMousePositionProvider _positionProvider = positionProvider;
    private int _lastX;
    private int _lastY;
    private int _pendingX;
    private int _pendingY;
    private bool _hasPosition;
    private bool _hasPendingPosition;

    public bool ProducesLogicalCoordinates => true;

    public bool ProducesRelativeCoordinates => true;

    public async Task InitializeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var position = await _positionProvider.GetAbsolutePositionAsync()
            .WaitAsync(ct)
            .ConfigureAwait(false);
        _lastX = position?.X ?? 0;
        _lastY = position?.Y ?? 0;
        _pendingX = _lastX;
        _pendingY = _lastY;
        _hasPosition = position is not null;
        _hasPendingPosition = false;
    }

    public CoordinateSample ProcessPosition(CapturedInputEvent e)
    {
        if (e.Type is InputEventType.MouseMove)
        {
            if (e.Code == InputEventCode.ABS_X)
            {
                _pendingX = e.Value;
                _hasPendingPosition = true;
            }
            else if (e.Code == InputEventCode.ABS_Y)
            {
                _pendingY = e.Value;
                _hasPendingPosition = true;
            }

            return CoordinateSample.None;
        }

        if (e.Type is InputEventType.Sync
            || (_hasPendingPosition && e.Type is InputEventType.MouseButton or InputEventType.MouseScroll))
        {
            return FlushPendingDelta();
        }

        return CoordinateSample.None;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private CoordinateSample FlushPendingDelta()
    {
        if (!_hasPendingPosition)
        {
            return CoordinateSample.None;
        }

        if (!_hasPosition)
        {
            _lastX = _pendingX;
            _lastY = _pendingY;
            _hasPosition = true;
            _hasPendingPosition = false;
            return CoordinateSample.None;
        }

        int deltaX = (int)Math.Clamp((long)_pendingX - _lastX, int.MinValue, int.MaxValue);
        int deltaY = (int)Math.Clamp((long)_pendingY - _lastY, int.MinValue, int.MaxValue);
        _lastX = _pendingX;
        _lastY = _pendingY;
        _hasPendingPosition = false;

        return deltaX is 0 && deltaY is 0
            ? CoordinateSample.None
            : CoordinateSample.Create(deltaX, deltaY);
    }
}
