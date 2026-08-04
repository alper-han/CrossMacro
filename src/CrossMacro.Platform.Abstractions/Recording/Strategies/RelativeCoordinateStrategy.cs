
namespace CrossMacro.Platform.Abstractions.Recording.Strategies;

/// <summary>
/// Relative coordinate strategy that buffers X/Y deltas until a SYNC event.
/// This ensures both axes are recorded together in a single MacroEvent.
/// </summary>
public sealed class RelativeCoordinateStrategy(bool producesLogicalCoordinates = false) : IRelativeCoordinateStrategy
{
    private long _pendingX;
    private long _pendingY;

    public bool ProducesLogicalCoordinates { get; } = producesLogicalCoordinates;

    public bool ProducesRelativeCoordinates => true;

    public Task InitializeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _pendingX = 0;
        _pendingY = 0;
        return Task.CompletedTask;
    }

    public CoordinateSample ProcessPosition(CapturedInputEvent e)
    {
        switch (e.Type)
        {
            case InputEventType.MouseMove:
                if (e.Code == InputEventCode.REL_X)
                {
                    _pendingX += e.Value;
                }
                else if (e.Code == InputEventCode.REL_Y)
                {
                    _pendingY += e.Value;
                }

                return CoordinateSample.None;

            case InputEventType.Sync:
                return FlushPendingDelta();

            case InputEventType.MouseButton:
            case InputEventType.MouseScroll:
            case InputEventType.Key:
                if (_pendingX is not 0 || _pendingY is not 0)
                {
                    return FlushPendingDelta();
                }

                return CoordinateSample.None;

            default:
                return CoordinateSample.None;
        }
    }

    private CoordinateSample FlushPendingDelta()
    {
        if (_pendingX is 0 && _pendingY is 0)
        {
            return CoordinateSample.None;
        }

        var sample = CoordinateSample.Create(
            (int)Math.Clamp(_pendingX, int.MinValue, int.MaxValue),
            (int)Math.Clamp(_pendingY, int.MinValue, int.MaxValue));
        _pendingX = 0;
        _pendingY = 0;
        return sample;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
