
namespace CrossMacro.Daemon.Services;

internal sealed class InputCaptureManager : IInputCaptureManager
{
    private static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(1);
    private readonly Dictionary<string, ILinuxCaptureReader> _readers = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private readonly Lock _reportForwardLock = new();
    private readonly Func<IReadOnlyList<InputDeviceHelper.InputDevice>> _deviceEnumerator;
    private readonly Func<IReadOnlyList<InputDeviceHelper.InputDevice>> _rescanDeviceEnumerator;
    private readonly Func<InputDeviceHelper.InputDevice, ILinuxCaptureReader> _readerFactory;
    private readonly TimeSpan _rescanInterval;
    private CancellationTokenSource? _rescanCancellation;
    private CaptureConfiguration? _configuration;

    public InputCaptureManager()
        : this(
            () => InputDeviceHelper.GetAvailableDevices(),
            device => new EvdevCaptureReaderAdapter(new EvdevReader(device.Path, device.Name)),
            () => InputDeviceHelper.GetAvailableDevices(logSummary: false))
    { /* Empty */ }

    internal InputCaptureManager(
        Func<IReadOnlyList<InputDeviceHelper.InputDevice>> deviceEnumerator,
        Func<InputDeviceHelper.InputDevice, ILinuxCaptureReader> readerFactory,
        Func<IReadOnlyList<InputDeviceHelper.InputDevice>>? rescanDeviceEnumerator = null,
        TimeSpan? rescanInterval = null)
    {
        _deviceEnumerator = deviceEnumerator ?? throw new ArgumentNullException(nameof(deviceEnumerator));
        _rescanDeviceEnumerator = rescanDeviceEnumerator ?? _deviceEnumerator;
        _readerFactory = readerFactory ?? throw new ArgumentNullException(nameof(readerFactory));
        _rescanInterval = rescanInterval ?? RescanInterval;
        if (_rescanInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(rescanInterval), "Rescan interval must be positive.");
        }
    }

    public CaptureStartResult StartCapture(bool captureMouse, bool captureKeyboard, Action<UInputNative.input_event> onEvent)
    {
        lock (_lock)
        {
            StopCapture_NoLock();

            var configuration = new CaptureConfiguration(captureMouse, captureKeyboard, onEvent);
            var targetDevices = GetTargetDevices(configuration, _deviceEnumerator);

            Log.Information("[InputCaptureManager] Starting capture on {Count} devices", targetDevices.Count);

            if (targetDevices.Count is 0)
            {
                return CaptureStartResult.Failed("No matching input devices found.");
            }

            AddReaders(targetDevices, configuration);

            if (_readers.Count is 0)
            {
                return CaptureStartResult.Failed("Failed to open any input devices.");
            }

            _configuration = configuration;
            var rescanCancellation = new CancellationTokenSource();
            _rescanCancellation = rescanCancellation;
            _ = Task.Run(() => RescanLoopAsync(rescanCancellation), CancellationToken.None);
            return CaptureStartResult.Started(_readers.Count);
        }
    }

    public void StopCapture()
    {
        lock (_lock)
        {
            StopCapture_NoLock();
        }
    }

    public void Dispose()
    {
        StopCapture();
    }

    private static IReadOnlyList<InputDeviceHelper.InputDevice> GetTargetDevices(
        CaptureConfiguration configuration,
        Func<IReadOnlyList<InputDeviceHelper.InputDevice>> deviceEnumerator)
    {
        return deviceEnumerator()
            .Where(device => DaemonInputCapturePolicy.ShouldCaptureDevice(
                device,
                configuration.CaptureMouse,
                configuration.CaptureKeyboard))
            .ToArray();
    }

    private async Task RescanLoopAsync(CancellationTokenSource cancellation)
    {
        var cancellationToken = cancellation.Token;
        using var timer = new PeriodicTimer(_rescanInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    lock (_lock)
                    {
                        if (_configuration is null || cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        ReconcileReaders(_configuration);
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.Warning(ex, "[InputCaptureManager] Input-device rescan failed; retrying on the next interval");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Capture stopped or replaced.
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private void ReconcileReaders(CaptureConfiguration configuration)
    {
        IReadOnlyList<InputDeviceHelper.InputDevice> devices;
        try
        {
            devices = GetTargetDevices(configuration, _rescanDeviceEnumerator);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[InputCaptureManager] Failed to enumerate input devices during rescan");
            return;
        }

        var targetPaths = devices.Select(device => device.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var (path, reader) in _readers.ToArray())
        {
            if (targetPaths.Contains(path) && reader.IsListening)
            {
                continue;
            }

            RemoveReader(path, reader);
        }

        AddReaders(devices, configuration);
    }

    private void AddReaders(IEnumerable<InputDeviceHelper.InputDevice> devices, CaptureConfiguration configuration)
    {
        foreach (var device in devices.Where(device => !_readers.ContainsKey(device.Path)))
        {
            ILinuxCaptureReader? reader = null;
            try
            {
                reader = _readerFactory(device);
                var reportAccumulator = new InputCaptureReportAccumulator(
                    configuration.CaptureMouse,
                    configuration.CaptureKeyboard);
                reader.EventReceived += (_, inputEvent) => ForwardCapturedEvent(
                    reportAccumulator,
                    configuration.OnEvent,
                    inputEvent);
                reader.Start();
                _readers.Add(device.Path, reader);
                Log.Information("[InputCaptureManager] Capturing {Name} ({Path})", device.Name, device.Path);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                DisposeReader(reader, device.Path);
                Log.Warning("Failed to open {Path}: {Msg}", device.Path, ex.Message);
            }
        }
    }

    private void ForwardCapturedEvent(
        InputCaptureReportAccumulator reportAccumulator,
        Action<UInputNative.input_event> onEvent,
        UInputNative.input_event inputEvent)
    {
        try
        {
            if (!reportAccumulator.TryAppend(inputEvent, out var completedReport))
            {
                return;
            }

            try
            {
                lock (_reportForwardLock)
                {
                    foreach (var reportEvent in completedReport!)
                    {
                        onEvent(reportEvent);
                    }
                }
            }
            finally
            {
                reportAccumulator.ResetCompletedReport();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Verbose(ex, "[InputCaptureManager] Error in event callback");
        }
    }

    private void StopCapture_NoLock()
    {
        var rescanCancellation = _rescanCancellation;
        _rescanCancellation = null;
        _configuration = null;
        rescanCancellation?.Cancel();

        if (_readers.Count is 0)
        {
            return;
        }

        foreach (var (path, reader) in _readers)
        {
            DisposeReader(reader, path);
        }
        _readers.Clear();
        Log.Information("[InputCaptureManager] Stopped capture");
    }

    private void RemoveReader(string path, ILinuxCaptureReader reader)
    {
        _ = _readers.Remove(path);
        DisposeReader(reader, path);
        Log.Information("[InputCaptureManager] Stopped capture for disconnected device {Path}", path);
    }

    private static void DisposeReader(ILinuxCaptureReader? reader, string path)
    {
        if (reader is null)
        {
            return;
        }

        try
        {
            reader.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning("Failed to dispose capture reader for {Path}: {Msg}", path, ex.Message);
        }
    }

    private sealed record CaptureConfiguration(
        bool CaptureMouse,
        bool CaptureKeyboard,
        Action<UInputNative.input_event> OnEvent);

    internal interface ILinuxCaptureReader : IDisposable
    {
        public event Action<ILinuxCaptureReader, UInputNative.input_event>? EventReceived;
        public bool IsListening { get; }
        public void Start();
    }

    private sealed class EvdevCaptureReaderAdapter : ILinuxCaptureReader
    {
        private readonly EvdevReader _reader;

        public EvdevCaptureReaderAdapter(EvdevReader reader)
        {
            _reader = reader;
            _reader.EventReceived += OnReaderEventReceived;
        }

        public event Action<ILinuxCaptureReader, UInputNative.input_event>? EventReceived;

        public bool IsListening => _reader.IsListening;

        public void Start() => _reader.Start();

        public void Dispose()
        {
            _reader.EventReceived -= OnReaderEventReceived;
            _reader.Dispose();
        }

        private void OnReaderEventReceived(object? sender, EvdevInputEventArgs e)
        {
            EventReceived?.Invoke(this, e.Event);
        }
    }
}
