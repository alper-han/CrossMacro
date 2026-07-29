
namespace CrossMacro.Platform.Linux.Ipc;

public sealed class LinuxIpcInputSimulator(IpcClient client, Func<bool>? isSupportedProbe = null) : IInputSimulator, IInputSimulatorCapabilities, IBatchedInputSimulator, IAsyncBatchedInputSimulator
{
    private IpcClient Client { get; } = client;
    private readonly Func<bool> _isSupportedProbe = isSupportedProbe ?? (static () => true);
    private bool _disposed;

    public string ProviderName => "Secure Daemon (UInput)";
    public bool IsSupported => !_disposed && (Client.IsConnected || IsProbeSupported());
    public bool SupportsAbsoluteCoordinates { get; private set; }
    public bool SupportsBatchedInput => !_disposed && Client.IsConnected;

    private const ushort EV_KEY = 0x01;
    private const ushort EV_REL = 0x02;
    private const ushort EV_ABS = 0x03;
    private const ushort EV_SYN = 0x00;

    private const ushort REL_X = 0x00;
    private const ushort REL_Y = 0x01;
    private const ushort REL_WHEEL = 0x08;
    private const ushort REL_HWHEEL = 0x06;

    private const ushort ABS_X = 0x00;
    private const ushort ABS_Y = 0x01;

    private const ushort SYN_REPORT = 0x00;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
        InitializeAsync(screenWidth, screenHeight).GetAwaiter().GetResult();
    }

    public async Task InitializeAsync(
        int screenWidth = 0,
        int screenHeight = 0,
        CancellationToken cancellationToken = default)
    {
        // Daemon initializes UInput lazy-loaded or on start. 
        // Resolution support would require protocol update.
        // For now, ignoring resolution, assuming relative movement mostly or default mapping.

        SupportsAbsoluteCoordinates = false;

        // Ensure connection
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

    public void MoveAbsolute(int x, int y)
    {
        Span<(ushort, ushort, int)> events =
        [
            (EV_ABS, ABS_X, x),
            (EV_ABS, ABS_Y, y),
            (EV_SYN, SYN_REPORT, 0),
        ];
        Client.SimulateEvents(events);
    }

    public void MoveRelative(int dx, int dy)
    {
        Span<(ushort, ushort, int)> events =
        [
            (EV_REL, REL_X, dx),
            (EV_REL, REL_Y, dy),
            (EV_SYN, SYN_REPORT, 0),
        ];
        Client.SimulateEvents(events);
    }

    public void MouseButton(int button, bool pressed)
    {
        Span<(ushort, ushort, int)> events =
        [
            (EV_KEY, (ushort)button, pressed ? 1 : 0),
            (EV_SYN, SYN_REPORT, 0),
        ];
        Client.SimulateEvents(events);
    }

    public void Scroll(int delta, bool isHorizontal = false)
    {
        ushort axis = isHorizontal ? REL_HWHEEL : REL_WHEEL;
        Span<(ushort, ushort, int)> events =
        [
            (EV_REL, axis, delta),
            (EV_SYN, SYN_REPORT, 0),
        ];
        Client.SimulateEvents(events);
    }

    public void KeyPress(int keyCode, bool pressed)
    {
        Span<(ushort, ushort, int)> events =
        [
            (EV_KEY, (ushort)keyCode, pressed ? 1 : 0),
            (EV_SYN, SYN_REPORT, 0),
        ];
        Client.SimulateEvents(events);
    }

    public void Sync()
    {
        Client.SimulateEvent(EV_SYN, SYN_REPORT, 0);
    }

    public void SimulateBatch(ReadOnlySpan<InputSimulationStep> steps)
    {
        Client.SimulateEventBatch(steps);
    }

    public Task SimulateBatchAsync(
        IReadOnlyList<InputSimulationStep> steps,
        CancellationToken cancellationToken = default) =>
        Client.SimulateEventBatchAsync(steps, cancellationToken);

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
        GC.SuppressFinalize(this);
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
}
