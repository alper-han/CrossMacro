
namespace CrossMacro.Daemon.Services;

internal sealed class VirtualDeviceManager : IVirtualDeviceManager, IAsyncDisposable
{
    private readonly Func<int, int, CancellationToken, Task<IUInputDevice>> _deviceFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Lock _disposeLock = new();
    private IUInputDevice? _uInputDevice;
    private (int Width, int Height)? _configuration;
    private (int X, int Y)? _lastAbsolutePosition;
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
            TrackAbsolutePosition(type, code, value);
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
            var prepared = PrepareEvents(events, out var resultingAbsolutePosition);
            foreach (var inputEvent in prepared)
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

            _lastAbsolutePosition = resultingAbsolutePosition;
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
        _lastAbsolutePosition = null;
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
        _lastAbsolutePosition = width > 0 && height > 0 ? (0, 0) : null;

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

    private IReadOnlyList<IpcSimulationRequest> PrepareEvents(
        IReadOnlyList<IpcSimulationRequest> events,
        out (int X, int Y)? resultingAbsolutePosition)
    {
        var absolutePosition = _lastAbsolutePosition;
        if (_configuration is not { } configuration || configuration.Width <= 0 || configuration.Height <= 0)
        {
            resultingAbsolutePosition = absolutePosition;
            return events;
        }

        var width = configuration.Width;
        var height = configuration.Height;

        var prepared = new List<IpcSimulationRequest>(events.Count + 3);
        var packetStart = 0;
        for (var index = 0; index < events.Count; index++)
        {
            var inputEvent = events[index];
            if (inputEvent.Type is not UInputNative.EV_SYN || inputEvent.Code is not UInputNative.SYN_REPORT)
            {
                continue;
            }

            if (TryGetAbsolutePacketTarget(events, packetStart, index, absolutePosition, out var target))
            {
                if (absolutePosition is { } previous
                    && target == previous
                    && TryGetReassertionPoint(target, width, height, out var reassertion))
                {
                    // Reassert repeated absolute targets through an adjacent point.
                    prepared.Add(new IpcSimulationRequest { Type = UInputNative.EV_ABS, Code = UInputNative.ABS_X, Value = reassertion.X });
                    prepared.Add(new IpcSimulationRequest { Type = UInputNative.EV_ABS, Code = UInputNative.ABS_Y, Value = reassertion.Y });
                    prepared.Add(new IpcSimulationRequest { Type = UInputNative.EV_SYN, Code = UInputNative.SYN_REPORT, Value = 0 });
                    prepared.Add(new IpcSimulationRequest { Type = UInputNative.EV_ABS, Code = UInputNative.ABS_X, Value = target.X });
                    prepared.Add(new IpcSimulationRequest { Type = UInputNative.EV_ABS, Code = UInputNative.ABS_Y, Value = target.Y });
                    prepared.Add(inputEvent);
                }
                else
                {
                    for (var packetIndex = packetStart; packetIndex <= index; packetIndex++)
                    {
                        prepared.Add(events[packetIndex]);
                    }
                }

                absolutePosition = target;
            }
            else
            {
                for (var packetIndex = packetStart; packetIndex <= index; packetIndex++)
                {
                    prepared.Add(events[packetIndex]);
                }

                absolutePosition = ApplyAbsoluteEvents(events, packetStart, index, absolutePosition);
            }

            packetStart = index + 1;
        }

        for (var index = packetStart; index < events.Count; index++)
        {
            prepared.Add(events[index]);
        }

        resultingAbsolutePosition = ApplyAbsoluteEvents(events, packetStart, events.Count, absolutePosition);
        return prepared;
    }

    private static bool TryGetAbsolutePacketTarget(
        IReadOnlyList<IpcSimulationRequest> events,
        int start,
        int syncIndex,
        (int X, int Y)? current,
        out (int X, int Y) target)
    {
        if (current is not { } initial)
        {
            target = default;
            return false;
        }

        target = initial;
        var hasAbsoluteAxis = false;
        for (var index = start; index < syncIndex; index++)
        {
            var inputEvent = events[index];
            if (inputEvent.DelayAfterMicroseconds is not 0
                || inputEvent.Type is not UInputNative.EV_ABS
                || inputEvent.Code is not (UInputNative.ABS_X or UInputNative.ABS_Y))
            {
                return false;
            }

            target = inputEvent.Code is UInputNative.ABS_X
                ? (inputEvent.Value, target.Y)
                : (target.X, inputEvent.Value);
            hasAbsoluteAxis = true;
        }

        return hasAbsoluteAxis;
    }

    private static (int X, int Y)? ApplyAbsoluteEvents(
        IReadOnlyList<IpcSimulationRequest> events,
        int start,
        int endExclusive,
        (int X, int Y)? current)
    {
        var position = current;
        for (var index = start; index < endExclusive; index++)
        {
            var inputEvent = events[index];
            if (position is not { } known || inputEvent.Type is not UInputNative.EV_ABS)
            {
                continue;
            }

            position = inputEvent.Code switch
            {
                UInputNative.ABS_X => (inputEvent.Value, known.Y),
                UInputNative.ABS_Y => (known.X, inputEvent.Value),
                _ => known,
            };
        }

        return position;
    }

    private static bool TryGetReassertionPoint((int X, int Y) target, int width, int height, out (int X, int Y) point)
    {
        if (width > 1)
        {
            point = target.X < width - 1 ? (target.X + 1, target.Y) : (target.X - 1, target.Y);
            return true;
        }

        if (height > 1)
        {
            point = target.Y < height - 1 ? (target.X, target.Y + 1) : (target.X, target.Y - 1);
            return true;
        }

        point = default;
        return false;
    }

    private void TrackAbsolutePosition(ushort type, ushort code, int value)
    {
        if (_lastAbsolutePosition is not { } position || type is not UInputNative.EV_ABS)
        {
            return;
        }

        _lastAbsolutePosition = code switch
        {
            UInputNative.ABS_X => (value, position.Y),
            UInputNative.ABS_Y => (position.X, value),
            _ => position,
        };
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
