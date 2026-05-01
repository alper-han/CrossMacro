using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.Strategies;

/// <summary>
/// Absolute coordinate strategy for Wayland that polls the compositor for real-time position.
/// Uses background sync loop + cache to avoid IPC latency in ProcessPosition().
/// </summary>
public class EvdevAbsoluteStrategy : ICoordinateStrategy
{
    private readonly IMousePositionProvider _positionProvider;
    private int _currentX;
    private int _currentY;
    private int _screenWidth = 1920;
    private int _screenHeight = 1080;
    
    private Task? _syncTask;
    private CancellationTokenSource? _cts;

    public EvdevAbsoluteStrategy(IMousePositionProvider positionProvider)
    {
        _positionProvider = positionProvider;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        // 1. Get Screen Resolution
        var res = await _positionProvider.GetScreenResolutionAsync();
        if (res.HasValue)
        {
            _screenWidth = res.Value.Width;
            _screenHeight = res.Value.Height;
            Log.Information("[EvdevAbsoluteStrategy] Screen resolution detected: {W}x{H}", _screenWidth, _screenHeight);
        }
        else
        {
            Log.Warning("[EvdevAbsoluteStrategy] Failed to detect screen resolution, defaulting to {W}x{H}", _screenWidth, _screenHeight);
        }

        // 2. Get Initial Position
        var pos = await _positionProvider.GetAbsolutePositionAsync();
        if (pos.HasValue)
        {
            _currentX = pos.Value.X;
            _currentY = pos.Value.Y;
            Log.Information("[EvdevAbsoluteStrategy] Initialized at ({X}, {Y})", _currentX, _currentY);
        }
        else
        {
            _currentX = _screenWidth / 2;
            _currentY = _screenHeight / 2;
            Log.Warning("[EvdevAbsoluteStrategy] Could not determine initial position. Defaulting to center ({X}, {Y}).", _currentX, _currentY);
        }

        // 3. Start background sync loop
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _syncTask = SyncLoopAsync(_cts.Token);
        Log.Information("[EvdevAbsoluteStrategy] Background position sync started (1ms polling)");
    }

    /// <summary>
    /// Background loop that continuously polls the compositor for real cursor position.
    /// This ensures we always have accurate coordinates without IPC latency in ProcessPosition.
    /// </summary>
    private async Task SyncLoopAsync(CancellationToken ct)
    {
        int errorCount = 0;
        const int maxErrors = 10;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pos = await _positionProvider.GetAbsolutePositionAsync();
                if (pos.HasValue)
                {
                    // Thread-safe update
                    Interlocked.Exchange(ref _currentX, pos.Value.X);
                    Interlocked.Exchange(ref _currentY, pos.Value.Y);
                    errorCount = 0; // Reset on success
                }

                // 1ms interval = 1000Hz polling for high-precision recording
                await Task.Delay(1, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                errorCount++;
                if (errorCount <= 3)
                {
                    Log.Warning(ex, "[EvdevAbsoluteStrategy] Sync loop error #{Count}", errorCount);
                }
                
                if (errorCount >= maxErrors)
                {
                    Log.Error("[EvdevAbsoluteStrategy] Too many sync errors, stopping background sync");
                    break;
                }

                // Back off on error
                await Task.Delay(100, ct);
            }
        }
        
        Log.Debug("[EvdevAbsoluteStrategy] Background sync loop ended");
    }

    public (int X, int Y) ProcessPosition(InputCaptureEventArgs e)
    {
        if (e.Type == InputEventType.Sync)
            return (0, 0);

        // Return cached position from background sync (zero latency)
        // Volatile.Read ensures we see the latest value written by the sync loop
        return (Volatile.Read(ref _currentX), Volatile.Read(ref _currentY));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        
        // Give sync loop a moment to exit gracefully
        try
        {
            _syncTask?.Wait(100);
        }
        catch { /* Ignore */ }
        
        _cts?.Dispose();
        Log.Debug("[EvdevAbsoluteStrategy] Disposed");
    }
}
