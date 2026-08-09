
namespace CrossMacro.Daemon.Services;

internal sealed class VirtualDeviceManager : IVirtualDeviceManager, IAsyncDisposable
{
    private readonly Func<int, int, CancellationToken, Task<IUInputDevice>> _deviceFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Lock _disposeLock = new();
    private IUInputDevice? _uInputDevice;
    private (int Width, int Height)? _configuration;
    private bool _disposed;
    private Task? _disposeTask;

    public VirtualDeviceManager()
        : this(CreateDeviceAsync)
    {
    }

    internal VirtualDeviceManager(
        Func<int, int, CancellationToken, Task<IUInputDevice>> deviceFactory)
    {
        ArgumentNullException.ThrowIfNull(deviceFactory);
        _deviceFactory = deviceFactory;
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Virtual input devices are supported only on Linux.");
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, GetOperationToken());
        await _gate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            linkedCts.Token.ThrowIfCancellationRequested();

            if (_uInputDevice is not null)
            {
                return;
            }

            await ReplaceDeviceAsync(width: 0, height: 0, linkedCts.Token).ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public async Task ConfigureAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Virtual input devices are supported only on Linux.");
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, GetOperationToken());
        await _gate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            linkedCts.Token.ThrowIfCancellationRequested();

            if (_uInputDevice is not null && _configuration is (var configuredWidth, var configuredHeight)
                && configuredWidth == width && configuredHeight == height)
            {
                Log.Debug(
                    "[VirtualDeviceManager] UInput device is already configured for {W}x{H}",
                    width,
                    height);
                return;
            }

            await ReplaceDeviceAsync(width, height, linkedCts.Token).ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public async Task SendEventAsync(ushort type, ushort code, int value, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Virtual input devices are supported only on Linux.");
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, GetOperationToken());
        await _gate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var device = _uInputDevice ?? throw new InvalidOperationException("The virtual input device is not initialized.");
            device.SendEvent(type, code, value);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public async Task SendEventsAsync(IReadOnlyList<IpcSimulationRequest> events, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Virtual input devices are supported only on Linux.");
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, GetOperationToken());
        await _gate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var device = _uInputDevice ?? throw new InvalidOperationException("The virtual input device is not initialized.");
            foreach (var inputEvent in events)
            {
                linkedCts.Token.ThrowIfCancellationRequested();
                device.SendEvent(inputEvent.Type, inputEvent.Code, inputEvent.Value);
                if (inputEvent.DelayAfterMicroseconds > 0)
                {
                    await DaemonPrecisionDelay.WaitAsync(
                        inputEvent.DelayAfterMicroseconds,
                        linkedCts.Token).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Virtual input devices are supported only on Linux.");
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, GetOperationToken());
        await _gate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        try
        {
            DisposeDevice();
            Log.Information("[VirtualDeviceManager] Device reset");
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            _disposeTask ??= DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        lock (_disposeLock)
        {
            _disposed = true;
        }

        await _disposeCts.CancelAsync().ConfigureAwait(false);
        var gateAcquired = await _gate.WaitAsync(Timeout.Infinite, CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (gateAcquired)
            {
                DisposeDevice();
            }
        }
        finally
        {
            if (gateAcquired)
            {
                _ = _gate.Release();
            }

            _gate.Dispose();
            _disposeCts.Dispose();
        }

    }

    private void DisposeDevice()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        _uInputDevice?.Dispose();
        _uInputDevice = null;
        _configuration = null;
    }

    private static async Task<IUInputDevice> CreateDeviceAsync(
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var device = new UInputDevice(width, height);
        try
        {
            await device.CreateVirtualInputDeviceAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return device;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            device.Dispose();
            throw;
        }
    }

    private async Task ReplaceDeviceAsync(int width, int height, CancellationToken cancellationToken)
    {
        IUInputDevice? newDevice = null;
        try
        {
            newDevice = await _deviceFactory(width, height, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            newDevice?.Dispose();
            Log.LogError(ex, "[VirtualDeviceManager] Failed to configure UInput device");
            throw;
        }

        var previousDevice = _uInputDevice;
        _uInputDevice = newDevice;
        _configuration = (width, height);

        try
        {
            previousDevice?.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[VirtualDeviceManager] Failed to dispose the previous UInput device");
        }

        Log.Information("[VirtualDeviceManager] Reconfigured UInput device with resolution {W}x{H}", width, height);
    }

    private CancellationToken GetOperationToken()
    {
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            return _disposeCts.Token;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
