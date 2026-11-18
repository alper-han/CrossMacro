using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions.Recording.Strategies;

namespace CrossMacro.Platform.MacOS.Strategies;

/// <summary>
/// Reports the absolute CoreGraphics cursor position: ABS_X/ABS_Y values are stored and emitted on the next sync.
/// </summary>
internal sealed class MacOSAbsoluteCoordinateStrategy(IMousePositionProvider positionProvider) : ICoordinateStrategy
{
    private readonly IMousePositionProvider _positionProvider = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
    private int _lastX;
    private int _lastY;
    private bool _hasPendingMovement;

    public bool ProducesLogicalCoordinates => true;

    public bool ProducesRelativeCoordinates => false;

    public async Task InitializeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _hasPendingMovement = false;

        try
        {
            var position = await _positionProvider.GetAbsolutePositionAsync().WaitAsync(ct).ConfigureAwait(false);
            if (position is { } current)
            {
                _lastX = current.X;
                _lastY = current.Y;
                return;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[MacOSAbsoluteCoordinateStrategy] Position provider query failed; using (0, 0)");
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

    public void Dispose() { /* The DI-owned position provider outlives this short-lived strategy. */ }
}
