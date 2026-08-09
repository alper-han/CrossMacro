
namespace CrossMacro.Platform.Linux;

public sealed class LinuxInputSimulator :
    IInputSimulator,
    IInputSimulatorCapabilities,
    IInputSimulatorAbsoluteBounds,
    IBatchedInputSimulator
{
    private readonly Func<int, int, IUInputDevice> _deviceFactory;
    private readonly Action<long> _waitMicroseconds;
    private IUInputDevice? _device;
    private bool _disposed;

    public LinuxInputSimulator()
        : this(static (width, height) => new UInputDevice(width, height), LinuxHighResolutionWait.Wait) { /* Empty */ }

    internal LinuxInputSimulator(
        Func<int, int, IUInputDevice> deviceFactory,
        Action<long>? waitMicroseconds = null)
    {
        _deviceFactory = deviceFactory ?? throw new ArgumentNullException(nameof(deviceFactory));
        _waitMicroseconds = waitMicroseconds ?? LinuxHighResolutionWait.Wait;
    }

    public string ProviderName => "Linux UInput";

    public bool IsSupported
    {
        get
        {
            try
            {
                return File.Exists(LinuxConstants.UInputDevicePath) || File.Exists(LinuxConstants.UInputAlternatePath);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return false;
            }
        }
    }

    public bool SupportsAbsoluteCoordinates => _device?.SupportsAbsoluteCoordinates ?? false;


    public bool UsesZeroBasedScreenBounds => true;

    public bool SupportsBatchedInput => _device is not null && !_disposed;

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
        if (_device is not null)
        {
            Log.Warning("[LinuxInputSimulator] Already initialized");
            return;
        }

        _device = _deviceFactory(screenWidth, screenHeight);
        _device.CreateVirtualInputDevice();
        Log.Information("[LinuxInputSimulator] Initialized with resolution {Width}x{Height}", screenWidth, screenHeight);
    }

    public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Initialize(screenWidth, screenHeight);
        return Task.CompletedTask;
    }

    public void MoveAbsolute(int x, int y)
    {
        _device?.MoveAbsolute(x, y);
    }

    public void MoveRelative(int dx, int dy)
    {
        _device?.Move(dx, dy);
    }

    public void MouseButton(int button, bool pressed)
    {
        _device?.EmitButton(button, pressed);
    }

    public void Scroll(int delta, bool isHorizontal = false)
    {
        ushort axis = isHorizontal ? UInputNative.REL_HWHEEL : UInputNative.REL_WHEEL;
        _device?.SendEvent(UInputNative.EV_REL, axis, delta);
        _device?.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
    }

    public void KeyPress(int keyCode, bool pressed)
    {
        _device?.EmitKey(keyCode, pressed);
    }

    public void Sync()
    {
        _device?.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
    }

    public void SimulateBatch(ReadOnlySpan<InputSimulationStep> steps)
    {
        ThrowIfDisposed();

        if (steps.IsEmpty)
        {
            return;
        }

        if (steps.Length > IpcProtocol.MaxSimulationBatchEvents)
        {
            throw new ArgumentOutOfRangeException(
                nameof(steps),
                $"Simulation batch contains {steps.Length.ToString(CultureInfo.InvariantCulture)} events, exceeding the maximum of {IpcProtocol.MaxSimulationBatchEvents.ToString(CultureInfo.InvariantCulture)}.");
        }

        var device = _device ?? throw new InvalidOperationException("Linux input simulator must be initialized before simulating batches.");
        long totalDelayMicroseconds = 0;

        foreach (var step in steps)
        {
            if (step.DelayAfterMicroseconds is < 0 or > IpcProtocol.MaxSimulationBatchDelayMicroseconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(steps),
                    $"Simulation batch delay {step.DelayAfterMicroseconds.ToString(CultureInfo.InvariantCulture)}us is outside the allowed range 0-{IpcProtocol.MaxSimulationBatchDelayMicroseconds.ToString(CultureInfo.InvariantCulture)}us.");
            }

            totalDelayMicroseconds += step.DelayAfterMicroseconds;
            if (totalDelayMicroseconds > IpcProtocol.MaxSimulationBatchTotalDelayMicroseconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(steps),
                    $"Simulation batch total delay {totalDelayMicroseconds.ToString(CultureInfo.InvariantCulture)}us exceeds the allowed maximum of {IpcProtocol.MaxSimulationBatchTotalDelayMicroseconds.ToString(CultureInfo.InvariantCulture)}us.");
            }

        }

        foreach (var step in steps)
        {
            device.SendEvent(step.Type, step.Code, step.Value);

            if (step.DelayAfterMicroseconds > 0)
            {
                _waitMicroseconds(step.DelayAfterMicroseconds);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _device?.Dispose();
            _device = null;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
