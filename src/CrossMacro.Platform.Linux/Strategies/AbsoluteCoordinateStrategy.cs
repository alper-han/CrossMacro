
namespace CrossMacro.Platform.Linux.Strategies;

/// <summary>
/// Consumes absolute root-window coordinates emitted by the X11 logical
/// capture path and publishes one atomic position for each sync event.
/// </summary>
public sealed class AbsoluteCoordinateStrategy(IMousePositionProvider positionProvider) : ICoordinateStrategy
{
    private IMousePositionProvider PositionProvider { get; } = positionProvider;
    private int _currentX;
    private int _currentY;
    private readonly Lock _lock = new();
    private bool _hasPendingMovement;

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
            _currentX = pos.Value.X;
            _currentY = pos.Value.Y;
            Log.Information("[AbsoluteCoordinateStrategy] Initialized at ({X}, {Y})", _currentX, _currentY);
        }
        else
        {
            Log.Warning("[AbsoluteCoordinateStrategy] Could not determine initial position. Defaulting to (0,0).");
            _currentX = 0;
            _currentY = 0;
        }

        _hasPendingMovement = false;

    }

    public CoordinateSample ProcessPosition(CapturedInputEvent e)
    {
        lock (_lock)
        {
            if (e.Type is InputEventType.Sync)
            {
                if (!_hasPendingMovement)
                {
                    return CoordinateSample.None;
                }

                _hasPendingMovement = false;
                return CoordinateSample.Create(_currentX, _currentY);
            }

            if (e.Type is InputEventType.MouseMove)
            {
                if (e.Code == InputEventCode.ABS_X)
                {
                    _currentX = e.Value;
                    _hasPendingMovement = true;
                }
                else if (e.Code == InputEventCode.ABS_Y)
                {
                    _currentY = e.Value;
                    _hasPendingMovement = true;
                }

                return CoordinateSample.None;
            }

            return CoordinateSample.Create(_currentX, _currentY);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
