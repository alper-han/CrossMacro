using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Diagnostics;
using CrossMacro.Core.Logging;

namespace CrossMacro.Core.Services;

/// <summary>
/// Manages pre-warmed IInputSimulator instances to eliminate device creation delays.
/// The pool creates devices in advance so they're ready immediately when needed.
/// </summary>
public class InputSimulatorPool : IDisposable
{
    private readonly Func<IInputSimulator> _factory;
    private readonly Lock _lock = new();
    
    private IInputSimulator? _warmRelativeDevice;
    private IInputSimulator? _warmAbsoluteDevice;
    private int _absoluteWidth;
    private int _absoluteHeight;
    private bool _disposed;
    
    private CancellationTokenSource? _warmUpCts;
    
    /// <summary>
    /// Indicates whether the pool has at least one warm device ready.
    /// </summary>
    public bool HasWarmDevice => _warmRelativeDevice != null || _warmAbsoluteDevice != null;
    
    public InputSimulatorPool(Func<IInputSimulator> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }
    
    /// <summary>
    /// Pre-warms devices for both relative and absolute modes.
    /// Call this at application startup for zero-delay playback.
    /// </summary>
    /// <param name="screenWidth">Screen width for absolute mode (0 for relative-only)</param>
    /// <param name="screenHeight">Screen height for absolute mode (0 for relative-only)</param>
    public async Task WarmUpAsync(int screenWidth = 0, int screenHeight = 0)
    {
        if (_disposed)
        {
            return;
        }

        Log.Information("[InputSimulatorPool] Warming up devices (resolution: {Width}x{Height})...", screenWidth, screenHeight);

        var warmUpCts = new CancellationTokenSource();
        var warmUpToken = warmUpCts.Token;
        var previousCts = Interlocked.Exchange(ref _warmUpCts, warmUpCts);
        previousCts?.Cancel();
        previousCts?.Dispose();

        try
        {
            await Task.Run(async () =>
            {
                if (_disposed || warmUpToken.IsCancellationRequested)
                {
                    return;
                }

                using (_lock.EnterScope())
                {
                    if (_disposed || warmUpToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (_warmRelativeDevice == null)
                    {
                        _warmRelativeDevice = _factory();
                        _warmRelativeDevice.Initialize(0, 0);
                        Log.Debug("[InputSimulatorPool] Relative device warmed up");
                    }
                }
                

                
                await Task.Delay(100, warmUpToken);

                if (_disposed || warmUpToken.IsCancellationRequested)
                {
                    return;
                }
                
                if (screenWidth > 0 && screenHeight > 0)
                {
                    using (_lock.EnterScope())
                    {
                        if (_disposed || warmUpToken.IsCancellationRequested)
                        {
                            return;
                        }

                        if (_warmAbsoluteDevice == null)
                        {
                            _warmAbsoluteDevice = _factory();
                            _warmAbsoluteDevice.Initialize(screenWidth, screenHeight);
                            _absoluteWidth = screenWidth;
                            _absoluteHeight = screenHeight;
                            Log.Debug("[InputSimulatorPool] Absolute device warmed up ({Width}x{Height})", screenWidth, screenHeight);
                        }
                    }
                }
                
                Log.Information("[InputSimulatorPool] Warm-up complete");
            }, warmUpToken);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("[InputSimulatorPool] Warm-up cancelled");
        }
        catch (ObjectDisposedException ex)
        {
            Log.Debug(ex, "[InputSimulatorPool] Warm-up skipped during disposal");
        }
        catch (Exception ex)
        {
            if (_disposed || warmUpToken.IsCancellationRequested)
            {
                Log.Debug(ex, "[InputSimulatorPool] Warm-up ended during shutdown");
            }
            else if (InputBackendErrorClassifier.IsKnownUnavailable(ex))
            {
                Log.Warning("[InputSimulatorPool] Warm-up skipped: {Error}", ex.Message);
            }
            else
            {
                Log.Error(ex, "[InputSimulatorPool] Failed to warm up devices");
            }
        }
    }
    
    /// <summary>
    /// Acquires an input simulator from the pool. Returns a pre-warmed device if available,
    /// otherwise creates a new one (with minimal delay since a replacement warm-up starts immediately).
    /// </summary>
    /// <param name="screenWidth">Screen width (0 for relative mode)</param>
    /// <param name="screenHeight">Screen height (0 for relative mode)</param>
    /// <returns>Ready-to-use IInputSimulator instance</returns>
    public IInputSimulator Acquire(int screenWidth, int screenHeight)
    {
        bool needsAbsolute = screenWidth > 0 && screenHeight > 0;
        IInputSimulator? device = null;
        
        using (_lock.EnterScope())
        {
            if (needsAbsolute)
            {
                if (_warmAbsoluteDevice != null && _absoluteWidth == screenWidth && _absoluteHeight == screenHeight)
                {
                    device = _warmAbsoluteDevice;
                    _warmAbsoluteDevice = null;
                    Log.Information("[InputSimulatorPool] Acquired warm absolute device ({Width}x{Height})", screenWidth, screenHeight);
                }
            }
            else
            {
                if (_warmRelativeDevice != null)
                {
                    device = _warmRelativeDevice;
                    _warmRelativeDevice = null;
                    Log.Information("[InputSimulatorPool] Acquired warm relative device");
                }
            }
        }
        
        if (device != null)
        {
            QueueWarmUpReplacement(screenWidth, screenHeight);
            return device;
        }
        
        Log.Warning("[InputSimulatorPool] No warm device available, creating new device (this will have a delay)");
        device = _factory();
        device.Initialize(screenWidth, screenHeight);
        
        return device;
    }
    
    /// <summary>
    /// Returns a device to the pool. Since UInput devices can't be reused after being
    /// associated with a specific configuration, this disposes the old device and
    /// starts warming up a fresh one.
    /// </summary>
    public void Release(IInputSimulator device, int screenWidth = 0, int screenHeight = 0)
    {
        try
        {
            device.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[InputSimulatorPool] Error disposing returned device");
        }

        QueueWarmUpReplacement(screenWidth, screenHeight);
    }

    private void QueueWarmUpReplacement(int screenWidth, int screenHeight)
    {
        if (_disposed)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await WarmUpReplacementAsync(screenWidth, screenHeight);
            }
            catch (Exception ex)
            {
                // Observe any unexpected faults from fire-and-forget replacement tasks.
                Log.Debug(ex, "[InputSimulatorPool] Replacement warm-up task faulted");
            }
        });
    }
    
    private async Task WarmUpReplacementAsync(int screenWidth, int screenHeight)
    {
        if (_disposed) return;
        
        try
        {
            bool needsAbsolute = screenWidth > 0 && screenHeight > 0;
            
            await Task.Delay(50);

            if (_disposed)
            {
                return;
            }
            
            using (_lock.EnterScope())
            {
                if (_disposed)
                {
                    return;
                }

                if (needsAbsolute)
                {
                    if (_warmAbsoluteDevice == null || _absoluteWidth != screenWidth || _absoluteHeight != screenHeight)
                    {
                        _warmAbsoluteDevice?.Dispose();
                        _warmAbsoluteDevice = _factory();
                        _warmAbsoluteDevice.Initialize(screenWidth, screenHeight);
                        _absoluteWidth = screenWidth;
                        _absoluteHeight = screenHeight;
                        Log.Debug("[InputSimulatorPool] Replacement absolute device warmed up");
                    }
                }
                else
                {
                    if (_warmRelativeDevice == null)
                    {
                        _warmRelativeDevice = _factory();
                        _warmRelativeDevice.Initialize(0, 0);
                        Log.Debug("[InputSimulatorPool] Replacement relative device warmed up");
                    }
                }
            }
        }
        catch (ObjectDisposedException ex)
        {
            Log.Debug(ex, "[InputSimulatorPool] Replacement warm-up skipped during disposal");
        }
        catch (Exception ex)
        {
            if (_disposed)
            {
                Log.Debug(ex, "[InputSimulatorPool] Replacement warm-up ended during shutdown");
            }
            else if (InputBackendErrorClassifier.IsKnownUnavailable(ex))
            {
                Log.Warning("[InputSimulatorPool] Replacement warm-up skipped: {Error}", ex.Message);
            }
            else
            {
                Log.Error(ex, "[InputSimulatorPool] Failed to warm up replacement device");
            }
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var warmUpCts = Interlocked.Exchange(ref _warmUpCts, null);
        warmUpCts?.Cancel();
        warmUpCts?.Dispose();
        
        using (_lock.EnterScope())
        {
            _warmRelativeDevice?.Dispose();
            _warmRelativeDevice = null;
            
            _warmAbsoluteDevice?.Dispose();
            _warmAbsoluteDevice = null;
        }
        
        Log.Information("[InputSimulatorPool] Disposed");
    }
}
