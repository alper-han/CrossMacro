namespace CrossMacro.Infrastructure.Services.Playback;

internal static class AbsoluteCursorPositionSynchronizer
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(4);
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromMilliseconds(250);
    private const int PositionTolerance = 1;

    public static Task<AbsoluteCursorSettleResult> WaitAsync(
        IMousePositionProvider? positionProvider,
        int expectedX,
        int expectedY,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null)
    {
        return WaitUntilAsync(
            positionProvider,
            position => Math.Abs((long)position.X - expectedX) <= PositionTolerance
                && Math.Abs((long)position.Y - expectedY) <= PositionTolerance,
            SettleTimeout,
            cancellationToken,
            timeProvider);
    }

    public static async Task<AbsoluteCursorSettleResult> WaitUntilAsync(
        IMousePositionProvider? positionProvider,
        Func<(int X, int Y), bool> isSettled,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(isSettled);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);

        if (positionProvider is null || !positionProvider.HasUsableAbsolutePosition())
        {
            return new AbsoluteCursorSettleResult(IsSettled: true, LastObservedPosition: null);
        }

        (int X, int Y)? lastObservedPosition = null;
        var clock = timeProvider ?? TimeProvider.System;
        var startedAt = clock.GetTimestamp();

        while (true)
        {
            var remaining = timeout - clock.GetElapsedTime(startedAt);
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var position = await QueryPositionAsync(
                positionProvider,
                remaining,
                cancellationToken).ConfigureAwait(false);
            lastObservedPosition = position;
            if (position is { } observed && isSettled(observed))
            {
                return new AbsoluteCursorSettleResult(IsSettled: true, observed);
            }

            remaining = timeout - clock.GetElapsedTime(startedAt);
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(
                remaining < PollInterval ? remaining : PollInterval,
                clock,
                cancellationToken).ConfigureAwait(false);
        }

        return new AbsoluteCursorSettleResult(IsSettled: false, lastObservedPosition);
    }

    private static async Task<(int X, int Y)?> QueryPositionAsync(
        IMousePositionProvider positionProvider,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            return await positionProvider.GetAbsolutePositionAsync()
                .WaitAsync(timeout, TimeProvider.System, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return null;
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
