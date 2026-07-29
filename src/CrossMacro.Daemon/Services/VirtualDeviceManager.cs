
namespace CrossMacro.Daemon.Services;

internal sealed class VirtualDeviceManager : IVirtualDeviceManager, IAsyncDisposable
{
    private UInputDevice? _uInputDevice;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Lock _disposeLock = new();
    private bool _disposed;
    private Task? _disposeTask;

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
            var newDevice = new UInputDevice(width, height);
            try
            {
                await newDevice.CreateVirtualInputDeviceAsync().ConfigureAwait(false);
                linkedCts.Token.ThrowIfCancellationRequested();

                var previousDevice = _uInputDevice;
                _uInputDevice = newDevice;
                previousDevice?.Dispose();
                Log.Information("[VirtualDeviceManager] Reconfigured UInput device with resolution {W}x{H}", width, height);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                newDevice.Dispose();
                Log.LogError(ex, "[VirtualDeviceManager] Failed to configure UInput device");
                throw;
            }
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
            _uInputDevice?.SendEvent(type, code, value);
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
            var device = _uInputDevice;
            if (device is null)
            {
                return;
            }

            foreach (var inputEvent in events)
            {
                linkedCts.Token.ThrowIfCancellationRequested();
                device.SendEvent(inputEvent.Type, inputEvent.Code, inputEvent.Value);
                if (inputEvent.DelayAfterMs > 0)
                {
                    await Task.Delay(inputEvent.DelayAfterMs, linkedCts.Token).ConfigureAwait(false);
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
            _uInputDevice?.Dispose();
            _uInputDevice = null;
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
