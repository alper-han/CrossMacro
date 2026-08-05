
namespace CrossMacro.Platform.MacOS.Strategies;

/// <summary>
/// macOS-specific pure absolute coordinate strategy.
/// Uses the injected position provider to get true absolute coordinates.
/// No delta accumulation, no hybrid approach - 100% pure absolute.
/// </summary>
public sealed class MacOSAbsoluteCoordinateStrategy : ICoordinateStrategy
{
    private readonly IMousePositionProvider _positionProvider;
    private readonly bool _ownsPositionProvider;
    private int _lastX;
    private int _lastY;
    private bool _hasPendingMovement;

    public MacOSAbsoluteCoordinateStrategy()
        : this(new MacOSMousePositionProvider(), ownsPositionProvider: true)
    { /* Compatibility constructor for direct callers. */ }

    public MacOSAbsoluteCoordinateStrategy(IMousePositionProvider positionProvider)
        : this(positionProvider, ownsPositionProvider: false)
    { /* The DI-owned provider outlives this short-lived strategy. */ }

    private MacOSAbsoluteCoordinateStrategy(IMousePositionProvider positionProvider, bool ownsPositionProvider)
    {
        _positionProvider = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
        _ownsPositionProvider = ownsPositionProvider;
    }

    public bool ProducesLogicalCoordinates => true;

    public bool ProducesRelativeCoordinates => false;

    public async Task InitializeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _hasPendingMovement = false;

        try
        {
            var position = await _positionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
            if (position is { } current)
            {
                _lastX = current.X;
                _lastY = current.Y;
                return;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Trace.TraceWarning($"[MacOSAbsoluteCoordinateStrategy] Position provider query failed; using (0, 0): {ex.Message}");
        }

        _lastX = 0;
        _lastY = 0;
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

    public void Dispose()
    {
        if (_ownsPositionProvider)
        {
            _positionProvider.Dispose();
        }
    }
}
