
namespace CrossMacro.Platform.Linux.Ipc;

public sealed class LinuxIpcInputSimulator(IpcClient client, Func<bool>? isSupportedProbe = null) :
    IInputSimulator,
    IInputSimulatorCapabilities,
    IInputSimulatorAbsoluteBounds,
    IBatchedInputSimulator,
    IAsyncBatchedInputSimulator,
    IAbsoluteMotionTrajectorySimulator,
    IInputSimulatorLeaseRefresher
{
    private IpcClient Client { get; } = client;
    private readonly Func<bool> _isSupportedProbe = isSupportedProbe ?? (static () => true);
    private bool _disposed;

    public string ProviderName => "Secure Daemon (UInput)";
    public bool IsSupported => !_disposed && (Client.IsConnected || IsProbeSupported());
    public bool SupportsAbsoluteCoordinates { get; private set; }
    public bool UsesZeroBasedScreenBounds => true;
    public bool SupportsBatchedInput => !_disposed && Client.IsConnected;

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    public async Task InitializeAsync(
        int screenWidth = 0,
        int screenHeight = 0,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        SupportsAbsoluteCoordinates = false;

        if (!Client.IsConnected)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(ConnectTimeout);
                await Client.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                Log.Warning("[LinuxIpcInputSimulator] Daemon connection timeout ({TimeoutMs}ms)", ConnectTimeout.TotalMilliseconds);
            }
            catch (IpcClientException ex) when (ex.Reason is IpcClientFailureReason.Timeout)
            {
                Log.Warning("[LinuxIpcInputSimulator] Daemon handshake timeout ({TimeoutMs}ms)", ConnectTimeout.TotalMilliseconds);
            }
            catch (IpcClientException ex)
            {
                Log.Warning(ex, "[LinuxIpcInputSimulator] Failed to connect to daemon ({Reason})", ex.Reason);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Warning(ex, "[LinuxIpcInputSimulator] Error during initialization");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (Client.IsConnected && screenWidth > 0 && screenHeight > 0)
        {
            Client.ConfigureResolution(screenWidth, screenHeight);
            SupportsAbsoluteCoordinates = true;
        }
    }

    Task IInputSimulatorLeaseRefresher.RefreshLeaseAsync(
        int screenWidth,
        int screenHeight,
        CancellationToken cancellationToken) =>
        InitializeAsync(screenWidth, screenHeight, cancellationToken);

    public void MoveAbsolute(int x, int y)
    {
        ThrowIfDisposed();
        // Keep UInputNative.ABS_X, UInputNative.ABS_Y and UInputNative.SYN_REPORT together and await daemon processing.
        InputSimulationStep[] events =
        [
            new(UInputNative.EV_ABS, UInputNative.ABS_X, x),
            new(UInputNative.EV_ABS, UInputNative.ABS_Y, y),
            new(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0),
        ];
        Client.SimulateEventBatch(events);
    }

    public void MoveRelative(int dx, int dy)
    {
        ThrowIfDisposed();
        Span<(ushort, ushort, int)> events =
        [
            (UInputNative.EV_REL, UInputNative.REL_X, dx),
            (UInputNative.EV_REL, UInputNative.REL_Y, dy),
            (UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0),
        ];
        Client.SimulateEvents(events);
    }

    public void MouseButton(int button, bool pressed)
    {
        ThrowIfDisposed();
        Span<(ushort, ushort, int)> events =
        [
            (UInputNative.EV_KEY, (ushort)button, pressed ? 1 : 0),
            (UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0),
        ];
        Client.SimulateEvents(events);
    }

    public void Scroll(int delta, bool isHorizontal = false)
    {
        ThrowIfDisposed();
        ushort axis = isHorizontal ? UInputNative.REL_HWHEEL : UInputNative.REL_WHEEL;
        Span<(ushort, ushort, int)> events =
        [
            (UInputNative.EV_REL, axis, delta),
            (UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0),
        ];
        Client.SimulateEvents(events);
    }

    public void KeyPress(int keyCode, bool pressed)
    {
        ThrowIfDisposed();
        Span<(ushort, ushort, int)> events =
        [
            (UInputNative.EV_KEY, (ushort)keyCode, pressed ? 1 : 0),
            (UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0),
        ];
        Client.SimulateEvents(events);
    }

    public void Sync()
    {
        ThrowIfDisposed();
        Client.SimulateEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
    }

    public void SimulateBatch(ReadOnlySpan<InputSimulationStep> steps)
    {
        ThrowIfDisposed();
        Client.SimulateEventBatch(steps);
    }

    public Task SimulateBatchAsync(
        IReadOnlyList<InputSimulationStep> steps,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return Client.SimulateEventBatchAsync(steps, cancellationToken);
    }

    public Task SimulateAbsoluteTrajectoryAsync(
        IReadOnlyList<AbsoluteMotionTrajectorySample> samples,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count is 0)
        {
            return Task.CompletedTask;
        }

        if (!SupportsAbsoluteCoordinates)
        {
            throw new InvalidOperationException("Absolute trajectory simulation requires configured absolute coordinates.");
        }

        if (samples.Count > IpcProtocol.MaxSimulationBatchEvents / 3)
        {
            throw new ArgumentOutOfRangeException(nameof(samples));
        }

        var steps = new InputSimulationStep[samples.Count * 3];
        var index = 0;
        foreach (var sample in samples)
        {
            if (sample.DelayAfterMicroseconds is < 0 or > IpcProtocol.MaxSimulationBatchDelayMicroseconds)
            {
                throw new ArgumentOutOfRangeException(nameof(samples));
            }

            steps[index++] = new InputSimulationStep(UInputNative.EV_ABS, UInputNative.ABS_X, sample.X);
            steps[index++] = new InputSimulationStep(UInputNative.EV_ABS, UInputNative.ABS_Y, sample.Y);
            steps[index++] = new InputSimulationStep(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0, sample.DelayAfterMicroseconds);
        }

        return Client.SimulateEventBatchAsync(steps, cancellationToken);
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private bool IsProbeSupported()
    {
        try
        {
            return _isSupportedProbe();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
