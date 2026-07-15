
namespace CrossMacro.Platform.Windows.Strategies;

/// <summary>
/// Windows-specific absolute coordinate strategy.
/// Uses GetCursorPos to get true absolute coordinates directly from Windows,
/// avoiding drift from accumulated relative deltas.
/// </summary>
public sealed class WindowsAbsoluteCoordinateStrategy : ICoordinateStrategy
{
    private readonly IMousePositionProvider _positionProvider;
    private int _lastX;
    private int _lastY;

    public WindowsAbsoluteCoordinateStrategy(IMousePositionProvider positionProvider)
    {
        _positionProvider = positionProvider;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var pos = await _positionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
        if (pos is not null)
        {
            _lastX = pos.Value.X;
            _lastY = pos.Value.Y;
        }
    }

    public (int X, int Y) ProcessPosition(CapturedInputEvent e)
    {
        if (e.Type is InputEventType.Sync)
        {
            return (0, 0);
        }

        if (e.Type is not InputEventType.MouseMove)
        {
            return (_lastX, _lastY);
        }

        if (User32.GetCursorPos(out POINT pt))
        {
            _lastX = pt.x;
            _lastY = pt.y;
        }
        else
        {
            Serilog.Log.Warning("[WindowsAbsoluteCoordinateStrategy] GetCursorPos failed, keeping last position ({X}, {Y})", _lastX, _lastY);
        }

        return (_lastX, _lastY);
    }

    public void Dispose()
    {
    }
}
