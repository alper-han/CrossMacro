using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Recording;

/// <summary>
/// Background position sync service that corrects cursor drift
/// Single Responsibility: Periodically queries actual cursor position and notifies on significant changes
/// </summary>
public class PositionSyncService : IPositionSyncService
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(1);

    private readonly IMousePositionProvider _positionProvider;

    private const int BaseSyncIntervalMs = 1;
    private const int MaxSyncIntervalMs = 500;
    private const int DriftThresholdPx = 2;

    private CancellationTokenSource? _cancellation;
    private Task? _syncTask;
    private readonly Lock _lock = new();
    private bool _disposed;

    public bool IsRunning => _syncTask != null && !_syncTask.IsCompleted;

    public PositionSyncService(IMousePositionProvider positionProvider)
    {
        _positionProvider = positionProvider;
    }

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

        Stop();

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
                        await Task.Delay(currentInterval, linkedCancellation.Token);

                        var sw = Stopwatch.StartNew();
                        var actualPos = await _positionProvider.GetAbsolutePositionAsync();
                        sw.Stop();

                        if (actualPos.HasValue)
                        {
                            var (cachedX, cachedY) = getCurrentPosition();

                            int driftX = Math.Abs(actualPos.Value.X - cachedX);
                            int driftY = Math.Abs(actualPos.Value.Y - cachedY);
                            int totalDrift = Math.Max(driftX, driftY);

                            if (totalDrift > DriftThresholdPx)
                            {
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
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[PositionSyncService] Error in sync loop");
                        consecutiveFailures++;
                    }
                }

                Log.Information("[PositionSyncService] Position sync stopped");
            }, linkedCancellation.Token);

            syncTask = _syncTask;
        }

        _ = ObserveSyncTaskAsync(syncTask, linkedCancellation);
        await Task.CompletedTask;
    }

    public void Stop()
    {
        Task? syncTask;
        CancellationTokenSource? cancellation;

        lock (_lock)
        {
            if (_cancellation == null && _syncTask == null)
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
        }

        if (syncTask == null)
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
        Stop();
    }

    private async Task ObserveSyncTaskAsync(Task syncTask, CancellationTokenSource cancellation)
    {
        try
        {
            await syncTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                if (ReferenceEquals(_syncTask, syncTask))
                {
                    _syncTask = null;
                    _cancellation = null;
                }
            }

            Log.Error(ex, "[PositionSyncService] Sync task faulted unexpectedly");
        }
    }

    private static async Task CompleteStopAsync(Task syncTask, CancellationTokenSource? cancellation)
    {
        try
        {
            var completedTask = await Task.WhenAny(syncTask, Task.Delay(StopTimeout)).ConfigureAwait(false);
            if (!ReferenceEquals(completedTask, syncTask))
            {
                Log.Warning("[PositionSyncService] Sync loop did not stop within {TimeoutMs}ms; shutdown will continue in background", StopTimeout.TotalMilliseconds);
            }

            await syncTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true)
        {
        }
        catch
        {
            // Faults are already handled by ObserveSyncTaskAsync.
        }
        finally
        {
            cancellation?.Dispose();
        }
    }
}
