using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Core.Services.Recording;

/// <summary>
/// Background position sync service that corrects cursor drift
/// Single Responsibility: Periodically queries actual cursor position and notifies on significant changes
/// </summary>
public class PositionSyncService : IPositionSyncService
{
    private readonly IMousePositionProvider _positionProvider;
    
    private const int BaseSyncIntervalMs = 1;
    private const int MaxSyncIntervalMs = 500;
    private const int DriftThresholdPx = 2;
    
    private CancellationTokenSource? _cancellation;
    private Task? _syncTask;
    
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
        if (!_positionProvider.IsSupported)
        {
            Log.Warning("[PositionSyncService] Position provider not supported, skipping sync");
            return;
        }
        
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        _syncTask = Task.Run(async () =>
        {
            int currentInterval = BaseSyncIntervalMs;
            int consecutiveFailures = 0;
            var stopwatch = Stopwatch.StartNew();
            
            Log.Information("[PositionSyncService] Position sync started (interval: {Interval}ms)", currentInterval);
            
            while (!_cancellation.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(currentInterval, _cancellation.Token);
                    
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
        }, _cancellation.Token);
    }
    
    public void Stop()
    {
        if (_cancellation != null)
        {
            _cancellation.Cancel();
            try
            {
                _syncTask?.Wait(1000);
            }
            catch (AggregateException) { }
            
            _cancellation.Dispose();
            _cancellation = null;
            _syncTask = null;
        }
    }
    
    public void Dispose()
    {
        Stop();
    }
}
