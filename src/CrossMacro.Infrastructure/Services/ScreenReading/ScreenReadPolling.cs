namespace CrossMacro.Infrastructure.Services.ScreenReading;

internal static class ScreenReadPolling
{
    private const int StableCenterTolerance = 2;
    private const int StableSizeTolerance = 1;

    public static DateTimeOffset GetDeadline(TimeSpan timeout, TimeProvider? timeProvider = null) =>
        (timeProvider ?? TimeProvider.System).GetUtcNow() + timeout;

    public static TimeSpan GetRemaining(DateTimeOffset deadline, TimeProvider? timeProvider = null)
    {
        var remaining = deadline - (timeProvider ?? TimeProvider.System).GetUtcNow();
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public static bool HasExpired(DateTimeOffset deadline, TimeProvider? timeProvider = null) =>
        (timeProvider ?? TimeProvider.System).GetUtcNow() >= deadline;

    public static TimeSpan GetDelay(DateTimeOffset deadline, TimeSpan pollInterval, TimeProvider? timeProvider = null)
    {
        var remaining = GetRemaining(deadline, timeProvider);
        return remaining < pollInterval ? remaining : pollInterval;
    }

    public static Task<ScreenReadResult<T>> PollUntilMatchAsync<T>(
        Func<TimeSpan, CancellationToken, Task<ScreenReadResult<T>>> searchOnceAsync,
        TimeSpan timeout,
        TimeSpan pollInterval,
        string canceledMessage,
        Func<ScreenReadResult<T>>? timeoutFailure,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(searchOnceAsync);
        ArgumentException.ThrowIfNullOrWhiteSpace(canceledMessage);
        return PollCoreAsync(
            searchOnceAsync,
            timeout,
            pollInterval,
            canceledMessage,
            consistency: null,
            timeoutFailure: timeoutFailure,
            incompleteMatchFailure: null,
            timeProvider: timeProvider ?? TimeProvider.System,
            cancellationToken: cancellationToken);
    }

    public static async Task<ScreenReadResult<ScreenImageMatch>> PollImageUntilConsistentAsync(
        Func<TimeSpan, CancellationToken, Task<ScreenReadResult<ScreenImageMatch>>> searchOnceAsync,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(searchOnceAsync);
        return await PollCoreAsync(
            searchOnceAsync,
            timeout,
            pollInterval,
            "Screen image polling was canceled.",
            timeout == TimeSpan.Zero ? null : IsConsistent,
            timeoutFailure: null,
            incompleteMatchFailure: static () => ScreenReadResultFactory.Failure<ScreenImageMatch>(
                ScreenReadErrorKind.CaptureTimeout,
                "Image was found in only one frame before the polling deadline."),
            timeProvider: timeProvider ?? TimeProvider.System,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ScreenReadResult<T>> PollCoreAsync<T>(
        Func<TimeSpan, CancellationToken, Task<ScreenReadResult<T>>> searchOnceAsync,
        TimeSpan timeout,
        TimeSpan pollInterval,
        string canceledMessage,
        Func<T, T, bool>? consistency,
        Func<ScreenReadResult<T>>? timeoutFailure,
        Func<ScreenReadResult<T>>? incompleteMatchFailure,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var deadline = GetDeadline(timeout, timeProvider);
        var hasPrevious = false;
        T? previous = default;
        ScreenReadResult<T>? lastFailure = null;

        while (true)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await searchOnceAsync(GetRemaining(deadline, timeProvider), cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    if (consistency is null
                        || (hasPrevious && consistency(previous!, result.Value!)))
                    {
                        return result;
                    }

                    previous = result.Value;
                    hasPrevious = true;
                }
                else
                {
                    hasPrevious = false;
                    previous = default;
                    if (result.ErrorKind is not ScreenReadErrorKind.CaptureTimeout)
                    {
                        return result;
                    }

                    lastFailure = result;
                }

                if (HasExpired(deadline, timeProvider))
                {
                    if (hasPrevious && incompleteMatchFailure is not null)
                    {
                        return incompleteMatchFailure();
                    }

                    return timeoutFailure?.Invoke()
                        ?? lastFailure
                        ?? ScreenReadResultFactory.Failure<T>(
                            ScreenReadErrorKind.CaptureTimeout,
                            "Screen read polling timed out.");
                }

                await Task.Delay(GetDelay(deadline, pollInterval, timeProvider), timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return ScreenReadResultFactory.Failure<T>(
                    ScreenReadErrorKind.Canceled,
                    canceledMessage);
            }
        }
    }

    private static bool IsConsistent(ScreenImageMatch first, ScreenImageMatch second)
    {
        var firstCenterX = first.Point.X + (first.MatchedWidth / 2.0);
        var firstCenterY = first.Point.Y + (first.MatchedHeight / 2.0);
        var secondCenterX = second.Point.X + (second.MatchedWidth / 2.0);
        var secondCenterY = second.Point.Y + (second.MatchedHeight / 2.0);
        return Math.Abs(firstCenterX - secondCenterX) <= StableCenterTolerance
            && Math.Abs(firstCenterY - secondCenterY) <= StableCenterTolerance
            && Math.Abs(first.MatchedWidth - second.MatchedWidth) <= StableSizeTolerance
            && Math.Abs(first.MatchedHeight - second.MatchedHeight) <= StableSizeTolerance;
    }
}
