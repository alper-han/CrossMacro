
namespace CrossMacro.Platform.Windows.Strategies;

/// <summary>
/// Windows-specific absolute coordinate strategy.
/// Uses GetCursorPos to get true absolute coordinates directly from Windows,
/// avoiding drift from accumulated relative deltas.
/// </summary>
public sealed class WindowsAbsoluteCoordinateStrategy(IMousePositionProvider positionProvider) : ICoordinateStrategy
{
    private IMousePositionProvider PositionProvider { get; } = positionProvider;
    private int _lastX;
    private int _lastY;
    private bool _hasPendingMovement;
    private bool _hasPendingAbsoluteCoordinate;

    public bool ProducesLogicalCoordinates => true;

    public bool ProducesRelativeCoordinates => false;

    public async Task InitializeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var pos = await PositionProvider.GetAbsolutePositionAsync()
            .WaitAsync(ct)
            .ConfigureAwait(false);
        if (pos is not null)
        {
            _lastX = pos.Value.X;
            _lastY = pos.Value.Y;
        }

        _hasPendingMovement = false;
        _hasPendingAbsoluteCoordinate = false;
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
            if (!_hasPendingAbsoluteCoordinate)
            {
                UpdateCursorPosition();
            }

            _hasPendingAbsoluteCoordinate = false;
            return CoordinateSample.Create(_lastX, _lastY);
        }

        if (e.Type is InputEventType.MouseMove)
        {
            if (e.Code == InputEventCode.ABS_X)
            {
                _lastX = e.Value;
                _hasPendingAbsoluteCoordinate = true;
            }
            else if (e.Code == InputEventCode.ABS_Y)
            {
                _lastY = e.Value;
                _hasPendingAbsoluteCoordinate = true;
            }

            _hasPendingMovement = true;
            return CoordinateSample.None;
        }

        return CoordinateSample.Create(_lastX, _lastY);
    }

    private void UpdateCursorPosition()
    {
        if (User32.GetCursorPos(out PointStruct pt))
        {
            _lastX = pt.x;
            _lastY = pt.y;
        }
        else
        {
            Serilog.Log.Warning("[WindowsAbsoluteCoordinateStrategy] GetCursorPos failed, keeping last position ({X}, {Y})", _lastX, _lastY);
        }
    }

    public void Dispose() { /* Empty */ }
}
