
namespace CrossMacro.Platform.Linux;

public sealed class LinuxInputCapture : IInputCapture, IAsyncDisposable
{
    private readonly List<ILinuxInputReader> _readers = new();
    private readonly Dictionary<ILinuxInputReader, List<UInputNative.input_event>> _pendingReports = new();
    private readonly Lock _reportForwardLock = new();
    private readonly Func<IReadOnlyList<InputDeviceHelper.InputDevice>> _deviceEnumerator;
    private readonly Func<InputDeviceHelper.InputDevice, ILinuxInputReader> _readerFactory;
    private bool _disposed;
    private CancellationTokenRegistration _stopRegistration;

    private bool _captureMouse = true;
    private bool _captureKeyboard = true;

    public string ProviderName => "Linux Evdev";

    public bool IsSupported
    {
        get
        {
            try
            {
                return Directory.Exists("/dev/input");
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Debug(ex, "[LinuxInputCapture] Failed to check /dev/input directory");
                return false;
            }
        }
    }

    public event EventHandler<CapturedInputEventArgs>? InputReceived;
    public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

    public LinuxInputCapture()
        : this(
            static () => InputDeviceHelper.GetAvailableDevices(),
            static device => new EvdevReaderAdapter(new EvdevReader(device.Path, device.Name)))
    { /* Empty */ }

    internal LinuxInputCapture(
        Func<IReadOnlyList<InputDeviceHelper.InputDevice>> deviceEnumerator,
        Func<InputDeviceHelper.InputDevice, ILinuxInputReader> readerFactory)
    {
        _deviceEnumerator = deviceEnumerator;
        _readerFactory = readerFactory;
    }

    public void Configure(bool captureMouse, bool captureKeyboard)
    {
        _captureMouse = captureMouse;
        _captureKeyboard = captureKeyboard;
        Log.Information("[LinuxInputCapture] Configured: Mouse={Mouse}, Keyboard={Keyboard}", captureMouse, captureKeyboard);
    }


    public async Task StartAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_readers.Count > 0)
        {
            Log.Warning("[LinuxInputCapture] Already started");
            return;
        }

        var nativeDevices = _deviceEnumerator();

        var devicesToUse = nativeDevices.Where(ShouldCaptureDevice).ToList();

        if (devicesToUse.Count is 0)
        {
            const string errorMsg = "No matching input devices found";
            Log.LogError("[LinuxInputCapture] {Error}", errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        Log.Information("[LinuxInputCapture] Starting capture on {Count} device(s):", devicesToUse.Count);

        foreach (var device in devicesToUse)
        {
            ILinuxInputReader? reader = null;
            try
            {
                reader = _readerFactory(device);
                reader.EventReceived += OnEvdevEventReceived;
                reader.ErrorOccurred += OnEvdevError;
                lock (_reportForwardLock)
                {
                    _pendingReports.Add(reader, new List<UInputNative.input_event>(capacity: 8));
                }

                reader.Start();
                _readers.Add(reader);
                Log.Information("[LinuxInputCapture]   - {Name} ({Path})", device.Name, device.Path);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (reader is not null)
                {
                    reader.EventReceived -= OnEvdevEventReceived;
                    reader.ErrorOccurred -= OnEvdevError;
                    lock (_reportForwardLock)
                    {
                        _ = _pendingReports.Remove(reader);
                    }

                    reader.Dispose();
                }

                Log.LogError(ex, "[LinuxInputCapture] Failed to open {Name}", device.Name);
            }
        }

        if (_readers.Count is 0)
        {
            const string errorMsg = "Failed to open any input devices";
            Log.LogError("[LinuxInputCapture] {Error}", errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        await _stopRegistration.DisposeAsync().ConfigureAwait(false);
        _stopRegistration = ct.Register(StopCapture);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public void StopCapture()
    {
        if (_readers.Count > 0)
        {
            foreach (var reader in _readers)
            {
                try
                {
                    reader.EventReceived -= OnEvdevEventReceived;
                    reader.ErrorOccurred -= OnEvdevError;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.Debug(ex, "[LinuxInputCapture] Error unsubscribing from reader events");
                }
            }

            _ = Parallel.ForEach(_readers, static reader =>
            {
                try
                {
                    reader.StopCapture();
                    reader.Dispose();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.LogError(ex, "[LinuxInputCapture] Error stopping reader");
                }
            });

            _readers.Clear();
            lock (_reportForwardLock)
            {
                _pendingReports.Clear();
            }
            Log.Information("[LinuxInputCapture] Stopped all readers");
        }

        _stopRegistration.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopCaptureAsync().ConfigureAwait(false);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async ValueTask StopCaptureAsync()
    {
        if (_readers.Count > 0)
        {
            foreach (var reader in _readers)
            {
                try
                {
                    reader.EventReceived -= OnEvdevEventReceived;
                    reader.ErrorOccurred -= OnEvdevError;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.Debug(ex, "[LinuxInputCapture] Error unsubscribing from reader events");
                }
            }

            await Task.WhenAll(_readers.Select(StopAndDisposeReaderAsync)).ConfigureAwait(false);
            _readers.Clear();
            lock (_reportForwardLock)
            {
                _pendingReports.Clear();
            }
            Log.Information("[LinuxInputCapture] Stopped all readers");
        }

        var stopRegistration = _stopRegistration;
        _stopRegistration = default;
        await stopRegistration.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task StopAndDisposeReaderAsync(ILinuxInputReader reader)
    {
        try
        {
            reader.StopCapture();
            await reader.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[LinuxInputCapture] Error stopping reader");
        }
    }

    private void OnEvdevEventReceived(ILinuxInputReader reader, UInputNative.input_event e)
    {
        lock (_reportForwardLock)
        {
            if (!_pendingReports.TryGetValue(reader, out var pendingReport))
            {
                return;
            }

            if (IsReportBoundary(e))
            {
                if (pendingReport.Count is 0)
                {
                    return;
                }

                pendingReport.Add(e);
                foreach (var reportEvent in pendingReport)
                {
                    ForwardEvdevEvent(reader, reportEvent);
                }

                pendingReport.Clear();
                return;
            }

            var eventType = MapEventType(e);
            if (ShouldForwardEvent(eventType))
            {
                pendingReport.Add(e);
            }
        }
    }

    private void ForwardEvdevEvent(ILinuxInputReader reader, UInputNative.input_event e)
    {
        var eventType = MapEventType(e);
        if (!ShouldForwardEvent(eventType))
        {
            return;
        }

        var args = new CapturedInputEvent
        {
            Type = eventType,
            Code = e.code,
            Value = e.value,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            TimestampMicroseconds = GetTimestampMicroseconds(e),
            DeviceName = reader.DeviceName,
        };

        InputReceived?.Invoke(this, new CapturedInputEventArgs(args));
    }

    private static long GetTimestampMicroseconds(UInputNative.input_event inputEvent)
    {
        var seconds = inputEvent.time_sec.ToInt64();
        var microseconds = inputEvent.time_usec.ToInt64();
        if ((seconds > 0 || microseconds > 0)
            && seconds >= 0
            && (microseconds is >= 0 and < 1_000_000))
        {
            try
            {
                return checked((seconds * 1_000_000) + microseconds);
            }
            catch (OverflowException)
            {
                // Fall back to the process clock.
            }
        }

        return Math.Max(1, GetStopwatchMicroseconds());
    }

    private static long GetStopwatchMicroseconds()
    {
        var timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        return checked((timestamp / System.Diagnostics.Stopwatch.Frequency * 1_000_000)
            + (timestamp % System.Diagnostics.Stopwatch.Frequency * 1_000_000
                / System.Diagnostics.Stopwatch.Frequency));
    }

    private static InputEventType MapEventType(UInputNative.input_event e)
    {
        var eventType = e.type switch
        {
            UInputNative.EV_KEY => UInputNative.IsMouseButton(e.code)
                ? InputEventType.MouseButton
                : InputEventType.Key,
            UInputNative.EV_REL => e.code is UInputNative.REL_WHEEL
                or UInputNative.REL_HWHEEL
                or UInputNative.REL_WHEEL_HI_RES
                or UInputNative.REL_HWHEEL_HI_RES
                ? InputEventType.MouseScroll
                : InputEventType.MouseMove,
            UInputNative.EV_ABS when e.code is UInputNative.ABS_X or UInputNative.ABS_Y
                => InputEventType.MouseMove,
            UInputNative.EV_SYN => InputEventType.Sync,
            _ => InputEventType.Unknown,
        };
        return eventType;
    }

    private static bool IsReportBoundary(UInputNative.input_event inputEvent)
    {
        return inputEvent.type == UInputNative.EV_SYN
            && inputEvent.code == UInputNative.SYN_REPORT;
    }

    private bool ShouldForwardEvent(InputEventType eventType)
    {
        return eventType switch
        {
            InputEventType.Key => _captureKeyboard,
            InputEventType.MouseButton => _captureMouse,
            InputEventType.MouseMove => _captureMouse,
            InputEventType.MouseScroll => _captureMouse,
            InputEventType.Sync => _captureMouse,
            InputEventType.Unknown => false,
            _ => false,
        };
    }

    private bool ShouldCaptureDevice(InputDeviceHelper.InputDevice device)
    {
        if (VirtualDeviceConstants.IsCrossMacroVirtualDevice(device.Name, device.VendorId, device.ProductId))
        {
            return false;
        }

        return (_captureMouse && device.IsMouse) || (_captureKeyboard && device.IsKeyboard);
    }


    private void OnEvdevError(Exception ex)
    {
        CaptureError?.Invoke(this, new InputCaptureErrorEventArgs(ex.Message));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopCapture();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    internal interface ILinuxInputReader : IDisposable, IAsyncDisposable
    {
        public string DeviceName { get; }
        public event Action<ILinuxInputReader, UInputNative.input_event>? EventReceived;
        public event Action<Exception>? ErrorOccurred;
        public void Start();
        public void StopCapture();
    }

    private sealed class EvdevReaderAdapter : ILinuxInputReader
    {
        private readonly EvdevReader _reader;

        public EvdevReaderAdapter(EvdevReader reader)
        {
            _reader = reader;
            _reader.EventReceived += OnReaderEventReceived;
            _reader.ErrorOccurred += OnReaderErrorOccurred;
        }

        public string DeviceName => _reader.DeviceName;

        public event Action<ILinuxInputReader, UInputNative.input_event>? EventReceived;
        public event Action<Exception>? ErrorOccurred;

        public void Start() => _reader.Start();

        public void StopCapture() => _reader.Stop();

        public void Dispose()
        {
            _reader.EventReceived -= OnReaderEventReceived;
            _reader.ErrorOccurred -= OnReaderErrorOccurred;
            _reader.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            _reader.EventReceived -= OnReaderEventReceived;
            _reader.ErrorOccurred -= OnReaderErrorOccurred;
            return _reader.DisposeAsync();
        }

        private void OnReaderEventReceived(object? sender, EvdevInputEventArgs e)
        {
            EventReceived?.Invoke(this, e.Event);
        }

        private void OnReaderErrorOccurred(object? sender, EvdevErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(e.Exception);
        }
    }
}
