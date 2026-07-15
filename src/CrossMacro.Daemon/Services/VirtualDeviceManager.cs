using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Linux.Native.UInput;
using CrossMacro.Core.Logging;

namespace CrossMacro.Daemon.Services;

public class VirtualDeviceManager : IVirtualDeviceManager
{
    private UInputDevice? _uInputDevice;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly object _disposeLock = new();
    private bool _disposed;
    
    public void Configure(int width, int height)
    {
        ConfigureAsync(width, height).GetAwaiter().GetResult();
    }

    public async Task ConfigureAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, GetOperationToken());
        await _gate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            try
            {
                _uInputDevice?.Dispose();
                _uInputDevice = new UInputDevice(width, height);
                await _uInputDevice.CreateVirtualInputDeviceAsync().ConfigureAwait(false);
                Log.Information("[VirtualDeviceManager] Reconfigured UInput device with resolution {W}x{H}", width, height);
            }
            catch (Exception ex)
            {
                _uInputDevice?.Dispose();
                _uInputDevice = null;
                Log.Error(ex, "[VirtualDeviceManager] Failed to configure UInput device");
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void SendEvent(ushort type, ushort code, int value)
    {
        SendEventAsync(type, code, value).GetAwaiter().GetResult();
    }

    public async Task SendEventAsync(ushort type, ushort code, int value, CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, GetOperationToken());
        await _gate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            _uInputDevice?.SendEvent(type, code, value);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void SendEvents(ReadOnlySpan<IpcSimulationRequest> events)
    {
        SendEventsAsync(events.ToArray()).GetAwaiter().GetResult();
    }

    public async Task SendEventsAsync(IReadOnlyList<IpcSimulationRequest> events, CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, GetOperationToken());
        await _gate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            var device = _uInputDevice;
            if (device == null) return;

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
            _gate.Release();
        }
    }

    public void Reset()
    {
        ResetAsync().GetAwaiter().GetResult();
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
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
            _gate.Release();
        }
    }

    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _disposeCts.Cancel();
        }

        _gate.Wait();
        try
        {
            _uInputDevice?.Dispose();
            _uInputDevice = null;
        }
        finally
        {
            _gate.Release();
        }

        GC.SuppressFinalize(this);
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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VirtualDeviceManager));
        }
    }
}
