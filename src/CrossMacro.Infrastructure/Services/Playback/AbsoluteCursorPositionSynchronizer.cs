namespace CrossMacro.Infrastructure.Services.Playback;

internal static class AbsoluteCursorPositionSynchronizer
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(4);
    private const int MaxAttempts = 8;
    private const int PositionTolerance = 1;

    public static async Task<AbsoluteCursorSettleResult> WaitAsync(
        IMousePositionProvider? positionProvider,
        int expectedX,
        int expectedY,
        CancellationToken cancellationToken)
    {
        if (positionProvider is null || !positionProvider.HasUsableAbsolutePosition())
        {
            return new AbsoluteCursorSettleResult(IsSettled: true, LastObservedPosition: null);
        }

        (int X, int Y)? lastObservedPosition = null;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var position = await QueryPositionAsync(positionProvider, cancellationToken).ConfigureAwait(false);
            lastObservedPosition = position;
            if (position is { } observed
                && Math.Abs((long)observed.X - expectedX) <= PositionTolerance
                && Math.Abs((long)observed.Y - expectedY) <= PositionTolerance)
            {
                return new AbsoluteCursorSettleResult(IsSettled: true, observed);
            }

            if (attempt + 1 < MaxAttempts)
            {
                await Task.Delay(PollInterval, TimeProvider.System, cancellationToken).ConfigureAwait(false);
            }
        }

        return new AbsoluteCursorSettleResult(IsSettled: false, lastObservedPosition);
    }

    private static async Task<(int X, int Y)?> QueryPositionAsync(
        IMousePositionProvider positionProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            return await positionProvider.GetAbsolutePositionAsync()
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[AbsoluteCursorPositionSynchronizer] Failed to read cursor position");
            return null;
        }
    }
}
