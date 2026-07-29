
namespace CrossMacro.Platform.Linux;

public sealed class LinuxInputSimulator : IInputSimulator, IInputSimulatorCapabilities, IBatchedInputSimulator
{
    private readonly Func<int, int, IUInputDevice> _deviceFactory;
    private IUInputDevice? _device;
    private bool _disposed;

    public LinuxInputSimulator()
        : this(static (width, height) => new UInputDevice(width, height)) { /* Empty */ }

    internal LinuxInputSimulator(Func<int, int, IUInputDevice> deviceFactory)
    {
        _deviceFactory = deviceFactory ?? throw new ArgumentNullException(nameof(deviceFactory));
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
        var totalDelayMs = 0;

        foreach (var step in steps)
        {
            if (step.DelayAfterMs is < 0 or > IpcProtocol.MaxSimulationBatchDelayMs)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(steps),
                    $"Simulation batch delay {step.DelayAfterMs.ToString(CultureInfo.InvariantCulture)}ms is outside the allowed range 0-{IpcProtocol.MaxSimulationBatchDelayMs.ToString(CultureInfo.InvariantCulture)}ms.");
            }

            totalDelayMs += step.DelayAfterMs;
            if (totalDelayMs > IpcProtocol.MaxSimulationBatchTotalDelayMs)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(steps),
                    $"Simulation batch total delay {totalDelayMs.ToString(CultureInfo.InvariantCulture)}ms exceeds the allowed maximum of {IpcProtocol.MaxSimulationBatchTotalDelayMs.ToString(CultureInfo.InvariantCulture)}ms.");
            }

        }

        foreach (var step in steps)
        {
            device.SendEvent(step.Type, step.Code, step.Value);

            if (step.DelayAfterMs > 0)
            {
                Thread.Sleep(step.DelayAfterMs);
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
