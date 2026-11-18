
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Kde;

/// <summary>
/// Runs one capture under a single deadline that covers every step the operation awaits.
/// </summary>
internal static class KWinCaptureDeadline
{
    /// <summary>
    /// Runs <paramref name="operation"/> with a token that is canceled by the caller or when
    /// <see cref="ScreenReadOptions.Timeout"/> elapses.
    /// </summary>
    /// <exception cref="OperationCanceledException">The caller canceled.</exception>
    /// <exception cref="TimeoutException">The deadline elapsed before the operation finished.</exception>
    public static async Task<T> RunAsync<T>(
        ScreenReadOptions options,
        TimeProvider timeProvider,
        Func<CancellationToken, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(operation);

        if (options.Timeout is not { } limit)
        {
            return await operation(options.CancellationToken).ConfigureAwait(false);
        }

        using var timeout = new CancellationTokenSource(limit, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken, timeout.Token);
        try
        {
            return await operation(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!options.CancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"KWin ScreenShot2 capture timed out after {limit.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s.",
                ex);
        }
    }
}
