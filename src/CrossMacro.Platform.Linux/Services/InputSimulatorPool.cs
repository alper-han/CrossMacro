
namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Manages pre-warmed IInputSimulator instances to eliminate device creation delays.
/// The pool creates devices in advance so they're ready immediately when needed.
/// </summary>
public sealed class InputSimulatorPool(Func<IInputSimulator> factory) : IInputSimulatorPool, IAsyncDisposable
{
    private readonly Func<IInputSimulator> _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly Lock _lock = new();
    private readonly HashSet<IInputSimulator> _leasedDevices = new();
    private readonly HashSet<Task> _replacementTasks = new();

    private IInputSimulator? _warmRelativeDevice;
    private IInputSimulator? _warmAbsoluteDevice;
    private int _absoluteWidth;
    private int _absoluteHeight;
    private bool _disposed;

    private CancellationTokenSource? _warmUpCts;
    private readonly CancellationTokenSource _shutdownCts = new();

    /// <summary>
    /// Indicates whether the pool has at least one warm device ready.
    /// </summary>
    public bool HasWarmDevice => _warmRelativeDevice is not null || _warmAbsoluteDevice is not null;

    /// <summary>Completes when replacement work already queued by the pool has settled.</summary>
    public Task Completion
    {
        get
        {
            Task[] tasks;
            using (_lock.EnterScope())
            {
                tasks = [.. _replacementTasks];
            }

            return tasks.Length is 0 ? Task.CompletedTask : Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// Pre-warms devices for both relative and absolute modes.
    /// Call this at application startup for zero-delay playback.
    /// </summary>
    /// <param name="screenWidth">Screen width for absolute mode (0 for relative-only)</param>
    /// <param name="screenHeight">Screen height for absolute mode (0 for relative-only)</param>
    public async Task WarmUpAsync(
        int screenWidth = 0,
        int screenHeight = 0,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        Log.Information("[InputSimulatorPool] Warming up devices (resolution: {Width}x{Height})...", screenWidth, screenHeight);

        var warmUpCts = new CancellationTokenSource();
        var warmUpToken = warmUpCts.Token;
        var previousCts = Interlocked.Exchange(ref _warmUpCts, warmUpCts);
        await (previousCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
        previousCts?.Dispose();

        using var linkedWarmUpCancellation = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(warmUpToken, cancellationToken)
            : null;
        var effectiveWarmUpToken = linkedWarmUpCancellation?.Token ?? warmUpToken;

        try
        {
            await RunWarmUpAsync(screenWidth, screenHeight, effectiveWarmUpToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("[InputSimulatorPool] Warm-up cancelled");
        }
        catch (ObjectDisposedException ex)
        {
            Log.Debug(ex, "[InputSimulatorPool] Warm-up skipped during disposal");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
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
                Log.LogError(ex, "[InputSimulatorPool] Failed to warm up devices");
            }
        }
    }

    private async Task RunWarmUpAsync(int screenWidth, int screenHeight, CancellationToken cancellationToken)
    {
        if (_disposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        bool needsRelativeDevice;
        using (_lock.EnterScope())
        {
            needsRelativeDevice = !_disposed && !cancellationToken.IsCancellationRequested && _warmRelativeDevice is null;
        }

        if (needsRelativeDevice)
        {
            var relativeDevice = _factory();
            await relativeDevice.InitializeAsync(0, 0, cancellationToken).ConfigureAwait(false);
            using (_lock.EnterScope())
            {
                if (_disposed || cancellationToken.IsCancellationRequested)
                {
                    relativeDevice.Dispose();
                    return;
                }

                if (_warmRelativeDevice is null)
                {
                    _warmRelativeDevice = relativeDevice;
                    Log.Debug("[InputSimulatorPool] Relative device warmed up");
                }
                else
                {
                    relativeDevice.Dispose();
                }
            }
        }

        await Task.Delay(100, cancellationToken).ConfigureAwait(false);

        if (_disposed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (screenWidth > 0 && screenHeight > 0)
        {
            bool needsAbsoluteDevice;
            using (_lock.EnterScope())
            {
                needsAbsoluteDevice = !_disposed && !cancellationToken.IsCancellationRequested && _warmAbsoluteDevice is null;
            }

            if (needsAbsoluteDevice)
            {
                var absoluteDevice = _factory();
                await absoluteDevice.InitializeAsync(screenWidth, screenHeight, cancellationToken).ConfigureAwait(false);
                using (_lock.EnterScope())
                {
                    if (_disposed || cancellationToken.IsCancellationRequested)
                    {
                        absoluteDevice.Dispose();
                        return;
                    }

                    if (_warmAbsoluteDevice is null)
                    {
                        _warmAbsoluteDevice = absoluteDevice;
                        _absoluteWidth = screenWidth;
                        _absoluteHeight = screenHeight;
                        Log.Debug("[InputSimulatorPool] Absolute device warmed up ({Width}x{Height})", screenWidth, screenHeight);
                    }
                    else
                    {
                        absoluteDevice.Dispose();
                    }
                }
            }
        }

        Log.Information("[InputSimulatorPool] Warm-up complete");
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
                if (_warmAbsoluteDevice is not null && _absoluteWidth == screenWidth && _absoluteHeight == screenHeight)
                {
                    device = _warmAbsoluteDevice;
                    _warmAbsoluteDevice = null;
                    _ = _leasedDevices.Add(device);
                    Log.Information("[InputSimulatorPool] Acquired warm absolute device ({Width}x{Height})", screenWidth, screenHeight);
                }
            }
            else
            {
                if (_warmRelativeDevice is not null)
                {
                    device = _warmRelativeDevice;
                    _warmRelativeDevice = null;
                    _ = _leasedDevices.Add(device);
                    Log.Information("[InputSimulatorPool] Acquired warm relative device");
                }
            }
        }

        if (device is not null)
        {
            QueueWarmUpReplacement(screenWidth, screenHeight);
            return device;
        }

        Log.Warning("[InputSimulatorPool] No warm device available, creating new device (this will have a delay)");
        device = _factory();
        device.Initialize(screenWidth, screenHeight);

        using (_lock.EnterScope())
        {
            if (_disposed)
            {
                device.Dispose();
                throw new ObjectDisposedException(nameof(InputSimulatorPool));
            }

            _ = _leasedDevices.Add(device);
        }

        return device;
    }

    public async Task<IInputSimulator> AcquireAsync(
        int screenWidth,
        int screenHeight,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool needsAbsolute = screenWidth > 0 && screenHeight > 0;
        IInputSimulator? device = null;

        using (_lock.EnterScope())
        {
            if (needsAbsolute && _warmAbsoluteDevice is not null && _absoluteWidth == screenWidth && _absoluteHeight == screenHeight)
            {
                device = _warmAbsoluteDevice;
                _warmAbsoluteDevice = null;
            }
            else if (!needsAbsolute && _warmRelativeDevice is not null)
            {
                device = _warmRelativeDevice;
                _warmRelativeDevice = null;
            }

            if (device is not null)
            {
                _ = _leasedDevices.Add(device);
            }
        }

        if (device is null)
        {
            device = _factory();
            await device.InitializeAsync(screenWidth, screenHeight, cancellationToken).ConfigureAwait(false);
            using (_lock.EnterScope())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    device.Dispose();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (_disposed)
                {
                    device.Dispose();
                    throw new ObjectDisposedException(nameof(InputSimulatorPool));
                }

                _ = _leasedDevices.Add(device);
            }
        }
        else
        {
            QueueWarmUpReplacement(screenWidth, screenHeight);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            using (_lock.EnterScope())
            {
                _ = _leasedDevices.Remove(device);
            }

            device.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }

        return device;
    }

    /// <summary>
    /// Returns a device to the pool. Since UInput devices can't be reused after being
    /// associated with a specific configuration, this disposes the old device and
    /// starts warming up a fresh one.
    /// </summary>
    public void Release(IInputSimulator device, int screenWidth = 0, int screenHeight = 0)
    {
        ArgumentNullException.ThrowIfNull(device);

        using (_lock.EnterScope())
        {
            if (!_leasedDevices.Remove(device))
            {
                return;
            }
        }

        try
        {
            device.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[InputSimulatorPool] Error disposing returned device");
        }

        if (!_disposed)
        {
            QueueWarmUpReplacement(screenWidth, screenHeight);
        }
    }

    private void QueueWarmUpReplacement(int screenWidth, int screenHeight)
    {
        if (_disposed)
        {
            return;
        }

        var task = Task.Run(async () =>
        {
            try
            {
                await WarmUpReplacementAsync(screenWidth, screenHeight, _shutdownCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Observe any unexpected faults from fire-and-forget replacement tasks.
                Log.Debug(ex, "[InputSimulatorPool] Replacement warm-up task faulted");
            }
        }, _shutdownCts.Token);

        using (_lock.EnterScope())
        {
            if (_disposed)
            {
                return;
            }

            _ = _replacementTasks.Add(task);
        }

        _ = task.ContinueWith(
            completedTask =>
            {
                _ = completedTask.Exception;
                using (_lock.EnterScope())
                {
                    _ = _replacementTasks.Remove(completedTask);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task WarmUpReplacementAsync(int screenWidth, int screenHeight, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            bool needsAbsolute = screenWidth > 0 && screenHeight > 0;

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);

            if (_disposed)
            {
                return;
            }

            bool needsDevice;
            using (_lock.EnterScope())
            {
                needsDevice = !_disposed && (needsAbsolute
                    ? _warmAbsoluteDevice is null || _absoluteWidth != screenWidth || _absoluteHeight != screenHeight
                    : _warmRelativeDevice is null);
            }

            if (!needsDevice)
            {
                return;
            }

            var device = _factory();
            await device.InitializeAsync(needsAbsolute ? screenWidth : 0, needsAbsolute ? screenHeight : 0, cancellationToken).ConfigureAwait(false);

            using (_lock.EnterScope())
            {
                if (_disposed)
                {
                    device.Dispose();
                    return;
                }

                if (needsAbsolute)
                {
                    if (_warmAbsoluteDevice is null || _absoluteWidth != screenWidth || _absoluteHeight != screenHeight)
                    {
                        _warmAbsoluteDevice?.Dispose();
                        _warmAbsoluteDevice = device;
                        _absoluteWidth = screenWidth;
                        _absoluteHeight = screenHeight;
                        Log.Debug("[InputSimulatorPool] Replacement absolute device warmed up");
                    }
                    else
                    {
                        device.Dispose();
                    }
                }
                else if (_warmRelativeDevice is null)
                {
                    _warmRelativeDevice = device;
                    Log.Debug("[InputSimulatorPool] Replacement relative device warmed up");
                }
                else
                {
                    device.Dispose();
                }
            }
        }
        catch (ObjectDisposedException ex)
        {
            Log.Debug(ex, "[InputSimulatorPool] Replacement warm-up skipped during disposal");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
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
                Log.LogError(ex, "[InputSimulatorPool] Failed to warm up replacement device");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var warmUpCts = Interlocked.Exchange(ref _warmUpCts, value: null);
        warmUpCts?.Cancel();
        warmUpCts?.Dispose();
        _shutdownCts.Cancel();

        using (_lock.EnterScope())
        {
            _warmRelativeDevice?.Dispose();
            _warmRelativeDevice = null;

            _warmAbsoluteDevice?.Dispose();
            _warmAbsoluteDevice = null;
        }

        _shutdownCts.Dispose();

        Log.Information("[InputSimulatorPool] Disposed");
    }

    public async ValueTask DisposeAsync()
    {
        var completion = Completion;
        Dispose();
        try
        {
            await completion.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Replacement warm-up tasks observe shutdown cancellation.
        }
    }
}
