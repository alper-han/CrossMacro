
namespace CrossMacro.Infrastructure.Services.Recording;

/// <summary>
/// Background position sync service that corrects cursor drift
/// Single Responsibility: Periodically queries actual cursor position and notifies on significant changes
/// </summary>
public sealed class PositionSyncService(IMousePositionProvider positionProvider) : IPositionSyncService
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(1);

    private readonly IMousePositionProvider _positionProvider = positionProvider;

    private const int BaseSyncIntervalMs = 1;
    private const int MaxSyncIntervalMs = 500;
    private const int DriftThresholdPx = 2;

    private CancellationTokenSource? _cancellation;
    private Task? _syncTask;
    private readonly Lock _lock = new();
    private bool _disposed;

    public bool IsRunning => _syncTask is not null && !_syncTask.IsCompleted;

    public async Task StartAsync(
        Action<int, int, long> onPositionChanged,
        Func<(int X, int Y)> getCurrentPosition,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_positionProvider.IsSupported)
        {
            Log.Warning("[PositionSyncService] Position provider not supported, skipping sync");
            return;
        }

        StopPositionSync();

        var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task syncTask;

        lock (_lock)
        {
            _cancellation = linkedCancellation;
            _syncTask = Task.Run(async () =>
            {
                int currentInterval = BaseSyncIntervalMs;
                int consecutiveFailures = 0;
                var stopwatch = Stopwatch.StartNew();

                Log.Information("[PositionSyncService] Position sync started (interval: {Interval}ms)", currentInterval);

                while (!linkedCancellation.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(currentInterval), TimeProvider.System, linkedCancellation.Token).ConfigureAwait(false);

                        var sw = Stopwatch.StartNew();
                        var actualPos = await _positionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
                        sw.Stop();

                        if (linkedCancellation.IsCancellationRequested)
                        {
                            break;
                        }

                        if (actualPos is not null)
                        {
                            var (cachedX, cachedY) = getCurrentPosition();

                            int driftX = Math.Abs(actualPos.Value.X - cachedX);
                            int driftY = Math.Abs(actualPos.Value.Y - cachedY);
                            int totalDrift = Math.Max(driftX, driftY);

                            if (totalDrift > DriftThresholdPx)
                            {
                                if (linkedCancellation.IsCancellationRequested)
                                {
                                    break;
                                }

                                Log.Debug("[PositionSyncService] Position change: ({OldX},{OldY}) -> ({NewX},{NewY}), drift={Drift}px",
                                    cachedX, cachedY, actualPos.Value.X, actualPos.Value.Y, totalDrift);

                                onPositionChanged(actualPos.Value.X, actualPos.Value.Y, stopwatch.ElapsedMilliseconds);
                            }

                            // Adaptive interval based on query time
                            if (sw.ElapsedMilliseconds > 50)
                            {
                                currentInterval = Math.Min(currentInterval + 50, MaxSyncIntervalMs);
                                Log.Debug("[PositionSyncService] Slow query ({Ms}ms), increasing interval to {Interval}ms",
                                    sw.ElapsedMilliseconds, currentInterval);
                            }
                            else if (currentInterval > BaseSyncIntervalMs && sw.ElapsedMilliseconds < 10)
                            {
                                currentInterval = Math.Max(currentInterval - 50, BaseSyncIntervalMs);
                            }

                            consecutiveFailures = 0;
                        }
                        else
                        {
                            consecutiveFailures++;
                            if (consecutiveFailures > 3)
                            {
                                currentInterval = Math.Min(currentInterval * 2, MaxSyncIntervalMs);
                                Log.Warning("[PositionSyncService] Query failed {Count} times, backing off to {Interval}ms",
                                    consecutiveFailures, currentInterval);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        Log.LogError(ex, "[PositionSyncService] Error in sync loop");
                        consecutiveFailures++;
                    }
                }

                Log.Information("[PositionSyncService] Position sync stopped");
            }, linkedCancellation.Token);

            syncTask = _syncTask;
        }

        _ = ObserveSyncTaskAsync(syncTask, linkedCancellation);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public void StopPositionSync()
    {
        Task? syncTask;
        CancellationTokenSource? cancellation;

        lock (_lock)
        {
            if (_cancellation is null && _syncTask is null)
            {
                return;
            }

            syncTask = _syncTask;
            cancellation = _cancellation;
            _syncTask = null;
            _cancellation = null;
        }

        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            Log.Debug("[PositionSyncService] Cancellation source was already disposed while stopping.");
        }

        if (syncTask is null)
        {
            cancellation?.Dispose();
            return;
        }

        _ = CompleteStopAsync(syncTask, cancellation);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopPositionSync();
    }

    private async Task ObserveSyncTaskAsync(Task syncTask, CancellationTokenSource cancellation)
    {
        try
        {
            await syncTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Log.Debug("[PositionSyncService] Sync loop canceled during shutdown.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            lock (_lock)
            {
                if (ReferenceEquals(_syncTask, syncTask))
                {
                    _syncTask = null;
                    _cancellation = null;
                }
            }

            Log.LogError(ex, "[PositionSyncService] Sync task faulted unexpectedly");
        }
    }

    private static async Task CompleteStopAsync(Task syncTask, CancellationTokenSource? cancellation)
    {
        try
        {
            var completedTask = await Task.WhenAny(syncTask, Task.Delay(StopTimeout, TimeProvider.System, CancellationToken.None)).ConfigureAwait(false);
            if (!ReferenceEquals(completedTask, syncTask))
            {
                Log.Warning("[PositionSyncService] Sync loop did not stop within {TimeoutMs}ms; shutdown will continue in background", StopTimeout.TotalMilliseconds);
                _ = DisposeCancellationWhenCompletedAsync(syncTask, cancellation);
                return;
            }

            await syncTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when ((cancellation?.IsCancellationRequested) is true)
        {
            Log.Debug("[PositionSyncService] Stop completed through cancellation.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Faults are already handled by ObserveSyncTaskAsync.
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    private static async Task DisposeCancellationWhenCompletedAsync(Task syncTask, CancellationTokenSource? cancellation)
    {
        try
        {
            await syncTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when ((cancellation?.IsCancellationRequested) is true)
        {
            Log.Debug("[PositionSyncService] Background cleanup completed through cancellation.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Faults are already handled by ObserveSyncTaskAsync.
        }
        finally
        {
            cancellation?.Dispose();
        }
    }
}
