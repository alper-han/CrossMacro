using CrossMacro.Platform.Abstractions.Recording.Strategies;

namespace CrossMacro.Platform.MacOS.Strategies;

/// <summary>
/// Converts macOS CoreGraphics absolute mouse samples into relative deltas.
/// </summary>
internal sealed class MacOSRelativeCoordinateStrategy(IMousePositionProvider positionProvider) : IRelativeCoordinateStrategy
{
    private readonly IMousePositionProvider _positionProvider = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
    private int _lastX;
    private int _lastY;
    private int _pendingX;
    private int _pendingY;
    private bool _hasPendingPosition;
    private bool _hasPendingX;
    private bool _hasPendingY;

    public bool ProducesLogicalCoordinates => true;

    public bool ProducesRelativeCoordinates => true;

    public async Task InitializeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var position = await _positionProvider.GetAbsolutePositionAsync().WaitAsync(ct).ConfigureAwait(false);
        if (position is not null)
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
    }

    public CoordinateSample ProcessPosition(CapturedInputEvent e)
    {
        if (e.Type is InputEventType.MouseMove)
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

            return CoordinateSample.None;
        }

        if (e.Type is InputEventType.Sync)
        {
            return FlushPendingDelta();
        }

        if (e.Type is InputEventType.MouseButton && _hasPendingX && _hasPendingY)
        {
            return FlushPendingDelta();
        }

        return CoordinateSample.None;
    }

    public void Dispose() { /* Empty */ }

    private CoordinateSample FlushPendingDelta()
    {
        if (!_hasPendingPosition)
        {
            return CoordinateSample.None;
        }

        int deltaX = (int)Math.Clamp((long)_pendingX - _lastX, int.MinValue, int.MaxValue);
        int deltaY = (int)Math.Clamp((long)_pendingY - _lastY, int.MinValue, int.MaxValue);

        _lastX = _pendingX;
        _lastY = _pendingY;
        _hasPendingPosition = false;
        _hasPendingX = false;
        _hasPendingY = false;

        return deltaX is 0 && deltaY is 0
            ? CoordinateSample.None
            : CoordinateSample.Create(deltaX, deltaY);
    }
}
