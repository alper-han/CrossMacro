
namespace CrossMacro.Daemon.Services;

internal sealed class InputCaptureManager : IInputCaptureManager
{
    private readonly List<ILinuxCaptureReader> _readers = new();
    private readonly Lock _lock = new();
    private readonly Lock _reportForwardLock = new();
    private readonly Func<IReadOnlyList<InputDeviceHelper.InputDevice>> _deviceEnumerator;
    private readonly Func<InputDeviceHelper.InputDevice, ILinuxCaptureReader> _readerFactory;

    public InputCaptureManager()
        : this(
            () => InputDeviceHelper.GetAvailableDevices(),
            device => new EvdevCaptureReaderAdapter(new EvdevReader(device.Path, device.Name)))
    { /* Empty */ }

    internal InputCaptureManager(
        Func<IReadOnlyList<InputDeviceHelper.InputDevice>> deviceEnumerator,
        Func<InputDeviceHelper.InputDevice, ILinuxCaptureReader> readerFactory)
    {
        _deviceEnumerator = deviceEnumerator ?? throw new ArgumentNullException(nameof(deviceEnumerator));
        _readerFactory = readerFactory ?? throw new ArgumentNullException(nameof(readerFactory));
    }

    public CaptureStartResult StartCapture(bool captureMouse, bool captureKeyboard, Action<UInputNative.input_event> onEvent)
    {
        lock (_lock)
        {
            StopCapture(); // Clear existing

            var devices = _deviceEnumerator();
            var targetDevices = devices.Where(d => DaemonInputCapturePolicy.ShouldCaptureDevice(d, captureMouse, captureKeyboard)).ToList();

            Log.Information("[InputCaptureManager] Starting capture on {Count} devices", targetDevices.Count);

            if (targetDevices.Count is 0)
            {
                return CaptureStartResult.Failed("No matching input devices found.");
            }

            foreach (var dev in targetDevices)
            {
                ILinuxCaptureReader? evReader = null;
                try
                {
                    evReader = _readerFactory(dev);
                    var reportAccumulator = new InputCaptureReportAccumulator(captureMouse, captureKeyboard);
                    evReader.EventReceived += (sender, e) =>
                    {
                        try
                        {
                            if (reportAccumulator.TryAppend(e, out var completedReport))
                            {
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
                        }
                        catch (Exception ex) when (ex is not OutOfMemoryException)
                        {
                            Log.Verbose(ex, "[InputCaptureManager] Error in event callback");
                        }
                    };
                    evReader.Start();
                    _readers.Add(evReader);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    try
                    {
                        evReader?.Dispose();
                    }
                    catch (Exception disposeException) when (disposeException is not OutOfMemoryException)
                    {
                        Log.Warning("Failed to dispose capture reader for {Path}: {Msg}", dev.Path, disposeException.Message);
                    }

                    Log.Warning("Failed to open {Path}: {Msg}", dev.Path, ex.Message);
                }
            }

            if (_readers.Count is 0)
            {
                return CaptureStartResult.Failed("Failed to open any input devices.");
            }

            return CaptureStartResult.Started(_readers.Count);
        }
    }

    public void StopCapture()
    {
        lock (_lock)
        {
            if (_readers.Count > 0)
            {
                foreach (var r in _readers)
                {
                    r.Dispose();
                }
                _readers.Clear();
                Log.Information("[InputCaptureManager] Stopped capture");
            }
        }
    }

    public void Dispose()
    {
        StopCapture();
    }

    internal interface ILinuxCaptureReader : IDisposable
    {
        public event Action<ILinuxCaptureReader, UInputNative.input_event>? EventReceived;
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
