
namespace CrossMacro.Platform.Linux.Strategies;

public class AbsoluteCoordinateStrategy : ICoordinateStrategy
{
    private readonly IMousePositionProvider _positionProvider;
    private int _currentX;
    private int _currentY;
    private CancellationTokenSource? _syncCts;
    private Task? _syncTask;
    private readonly Lock _lock = new();

    public AbsoluteCoordinateStrategy(IMousePositionProvider positionProvider)
    {
        _positionProvider = positionProvider;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var pos = await _positionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
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

        _syncCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _syncTask = Task.Run(() => SyncLoop(_syncCts.Token));
    }

    private async Task SyncLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(100, token).ConfigureAwait(false);
                var pos = await _positionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
                if (pos is not null)
                {
                    lock (_lock)
                    {
                        if (Math.Abs(pos.Value.X - _currentX) > 5 || Math.Abs(pos.Value.Y - _currentY) > 5)
                        {
                            _currentX = pos.Value.X;
                            _currentY = pos.Value.Y;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected when the sync loop is cancelled via the cancellation token.
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "[AbsoluteCoordinateStrategy] Sync loop error");
        }
    }

    public (int X, int Y) ProcessPosition(CapturedInputEvent e)
    {
        lock (_lock)
        {
            if (e.Type is InputEventType.Sync)
            {
                return (0, 0);
            }

            if (e.Type is InputEventType.MouseMove)
            {
                if (e.Code == InputEventCode.REL_X)
                {
                    _currentX += e.Value;
                }
                else if (e.Code == InputEventCode.REL_Y)
                {
                    _currentY += e.Value;
                }
            }

            return (_currentX, _currentY);
        }
    }

    public void Dispose()
    {
        _syncCts?.Cancel();
        _syncCts?.Dispose();
    }
}
