namespace CrossMacro.Platform.Linux.Services;

public sealed class InputSimulatorPool(Func<IInputSimulator> factory) : IInputSimulatorPool, IAsyncDisposable
{
    private readonly Func<IInputSimulator> _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly Lock _lock = new();
    private readonly Dictionary<IInputSimulator, DeviceConfiguration> _leasedDevices = [];
    private readonly CancellationTokenSource _shutdown = new();

    private IInputSimulator? _warmRelativeDevice;
    private IInputSimulator? _warmAbsoluteDevice;
    private DeviceConfiguration? _warmAbsoluteConfiguration;
    private CancellationTokenSource? _warmUpCts;
    private bool _disposed;

    public bool HasWarmDevice
    {
        get
        {
            using (_lock.EnterScope())
            {
                return _warmRelativeDevice is not null || _warmAbsoluteDevice is not null;
            }
        }
    }

    public Task Completion => Task.CompletedTask;

    public async Task WarmUpAsync(
        int screenWidth = 0,
        int screenHeight = 0,
        CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? previousWarmUp;
        CancellationTokenSource warmUp;
        using (_lock.EnterScope())
        {
            if (_disposed || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            warmUp = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, cancellationToken);
            previousWarmUp = _warmUpCts;
            _warmUpCts = warmUp;
        }

        await (previousWarmUp?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
        previousWarmUp?.Dispose();

        try
        {
            await EnsureWarmAsync(DeviceConfiguration.Relative, warmUp.Token).ConfigureAwait(false);
            if (screenWidth > 0 && screenHeight > 0)
            {
                await EnsureWarmAsync(new DeviceConfiguration(screenWidth, screenHeight), warmUp.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (warmUp.IsCancellationRequested)
        {
            Log.Debug("[InputSimulatorPool] Warm-up cancelled");
        }
        catch (ObjectDisposedException ex)
        {
            Log.Debug(ex, "[InputSimulatorPool] Warm-up skipped during disposal");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (InputBackendErrorClassifier.IsKnownUnavailable(ex))
            {
                Log.Warning("[InputSimulatorPool] Warm-up skipped: {Error}", ex.Message);
            }
            else
            {
                Log.LogError(ex, "[InputSimulatorPool] Failed to warm up input simulator");
            }
        }
        finally
        {
            using (_lock.EnterScope())
            {
                if (ReferenceEquals(_warmUpCts, warmUp))
                {
                    _warmUpCts = null;
                }
            }

            warmUp.Dispose();
        }
    }

    public IInputSimulator Acquire(int screenWidth, int screenHeight)
    {
        var configuration = new DeviceConfiguration(screenWidth, screenHeight);
        var device = TakeWarmDevice(configuration);
        if (device is not null)
        {
            try
            {
                RefreshLease(device, configuration);
                return Lease(device, configuration);
            }
            catch
            {
                DisposeDevice(device);
                throw;
            }
        }

        var created = _factory();
        try
        {
            created.Initialize(screenWidth, screenHeight);
            return Lease(created, configuration);
        }
        catch
        {
            created.Dispose();
            throw;
        }
    }

    public async Task<IInputSimulator> AcquireAsync(
        int screenWidth,
        int screenHeight,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var configuration = new DeviceConfiguration(screenWidth, screenHeight);
        var device = TakeWarmDevice(configuration);
        if (device is not null)
        {
            try
            {
                await RefreshLeaseAsync(device, configuration, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return Lease(device, configuration);
            }
            catch
            {
                DisposeDevice(device);
                throw;
            }
        }

        var created = _factory();
        try
        {
            await created.InitializeAsync(screenWidth, screenHeight, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return Lease(created, configuration);
        }
        catch
        {
            created.Dispose();
            throw;
        }
    }

    public void Release(IInputSimulator device, int screenWidth = 0, int screenHeight = 0)
    {
        ArgumentNullException.ThrowIfNull(device);

        DeviceConfiguration? leasedConfiguration;
        using (_lock.EnterScope())
        {
            if (!_leasedDevices.Remove(device, out leasedConfiguration) || leasedConfiguration is null)
            {
                return;
            }
        }

        var configuration = leasedConfiguration;
        bool keep;
        using (_lock.EnterScope())
        {
            keep = !_disposed && StoreWarmDevice(device, configuration);
        }

        if (keep)
        {
            Log.Debug("[InputSimulatorPool] Returned {Mode} device to the warm pool", configuration.IsAbsolute ? "absolute" : "relative");
            return;
        }

        DisposeDevice(device);
    }

    private async Task EnsureWarmAsync(DeviceConfiguration configuration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = TakeWarmDevice(configuration);
        if (existing is not null)
        {
            ReturnWarmDevice(existing, configuration);
            return;
        }

        var created = _factory();
        try
        {
            await created.InitializeAsync(configuration.Width, configuration.Height, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            created.Dispose();
            throw;
        }

        bool retained;
        using (_lock.EnterScope())
        {
            retained = !_disposed && StoreWarmDevice(created, configuration);
        }

        if (!retained)
        {
            DisposeDevice(created);
        }
    }

    private IInputSimulator? TakeWarmDevice(DeviceConfiguration configuration)
    {
        IInputSimulator? stale = null;
        IInputSimulator? device = null;

        using (_lock.EnterScope())
        {
            ThrowIfDisposed();

            if (configuration.IsAbsolute)
            {
                if (_warmAbsoluteDevice is not null && _warmAbsoluteConfiguration == configuration)
                {
                    device = _warmAbsoluteDevice;
                    _warmAbsoluteDevice = null;
                    _warmAbsoluteConfiguration = null;
                }
                else if (_warmAbsoluteDevice is not null)
                {
                    stale = _warmAbsoluteDevice;
                    _warmAbsoluteDevice = null;
                    _warmAbsoluteConfiguration = null;
                }
            }
            else if (_warmRelativeDevice is not null)
            {
                device = _warmRelativeDevice;
                _warmRelativeDevice = null;
            }
        }

        DisposeDevice(stale);
        return device;
    }

    private IInputSimulator Lease(IInputSimulator device, DeviceConfiguration configuration)
    {
        using (_lock.EnterScope())
        {
            if (_disposed)
            {
                DisposeDevice(device);
                throw new ObjectDisposedException(nameof(InputSimulatorPool));
            }

            _leasedDevices.Add(device, configuration);
        }

        Log.Debug("[InputSimulatorPool] Acquired warm {Mode} device", configuration.IsAbsolute ? "absolute" : "relative");
        return device;
    }

    private static void RefreshLease(IInputSimulator device, DeviceConfiguration configuration)
    {
        if (device is IInputSimulatorLeaseRefresher refresher)
        {
            refresher.RefreshLeaseAsync(configuration.Width, configuration.Height, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }
    }

    private static Task RefreshLeaseAsync(
        IInputSimulator device,
        DeviceConfiguration configuration,
        CancellationToken cancellationToken) =>
        device is IInputSimulatorLeaseRefresher refresher
            ? refresher.RefreshLeaseAsync(configuration.Width, configuration.Height, cancellationToken)
            : Task.CompletedTask;

    private bool StoreWarmDevice(IInputSimulator device, DeviceConfiguration configuration)
    {
        if (configuration.IsAbsolute)
        {
            if (_warmAbsoluteDevice is not null)
            {
                return false;
            }

            _warmAbsoluteDevice = device;
            _warmAbsoluteConfiguration = configuration;
            return true;
        }

        if (_warmRelativeDevice is not null)
        {
            return false;
        }

        _warmRelativeDevice = device;
        return true;
    }

    private void ReturnWarmDevice(IInputSimulator device, DeviceConfiguration configuration)
    {
        bool retained;
        using (_lock.EnterScope())
        {
            retained = !_disposed && StoreWarmDevice(device, configuration);
        }

        if (!retained)
        {
            DisposeDevice(device);
        }
    }

    public void Dispose()
    {
        List<IInputSimulator> devices;
        CancellationTokenSource? warmUp;

        using (_lock.EnterScope())
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            warmUp = _warmUpCts;
            _warmUpCts = null;
            devices = [.. _leasedDevices.Keys];
            if (_warmRelativeDevice is not null)
            {
                devices.Add(_warmRelativeDevice);
            }

            if (_warmAbsoluteDevice is not null)
            {
                devices.Add(_warmAbsoluteDevice);
            }
            _leasedDevices.Clear();
            _warmRelativeDevice = null;
            _warmAbsoluteDevice = null;
            _warmAbsoluteConfiguration = null;
        }

        warmUp?.Cancel();
        warmUp?.Dispose();
        _shutdown.Cancel();
        _shutdown.Dispose();

        foreach (var device in devices)
        {
            DisposeDevice(device);
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static void DisposeDevice(IInputSimulator? device)
    {
        if (device is null)
        {
            return;
        }

        try
        {
            device.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[InputSimulatorPool] Failed to dispose input simulator");
        }
    }

    private sealed record DeviceConfiguration(int Width, int Height)
    {
        public static DeviceConfiguration Relative => new(0, 0);
        public bool IsAbsolute => Width > 0 && Height > 0;
    }
}
