using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Native.Evdev;
using CrossMacro.Platform.Linux.Native.UInput;
using Serilog;

namespace CrossMacro.Platform.Linux;

public class LinuxInputCapture : IInputCapture
{
    private readonly List<ILinuxInputReader> _readers = new();
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
            catch (Exception ex)
            {
                Log.Debug(ex, "[LinuxInputCapture] Failed to check /dev/input directory");
                return false;
            }
        }
    }
    
    public event EventHandler<InputCaptureEventArgs>? InputReceived;
    public event EventHandler<string>? Error;

    public LinuxInputCapture()
        : this(
            () => InputDeviceHelper.GetAvailableDevices(),
            device => new EvdevReaderAdapter(new EvdevReader(device.Path, device.Name)))
    {
    }

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
        
        if (devicesToUse.Count == 0)
        {
            var errorMsg = "No matching input devices found";
            Log.Error("[LinuxInputCapture] {Error}", errorMsg);
            throw new InvalidOperationException(errorMsg);
        }
        
        Log.Information("[LinuxInputCapture] Starting capture on {Count} device(s):", devicesToUse.Count);
        
        foreach (var device in devicesToUse)
        {
            try
            {
                var reader = _readerFactory(device);
                reader.EventReceived += OnEvdevEventReceived;
                reader.ErrorOccurred += OnEvdevError;
                reader.Start();
                _readers.Add(reader);
                Log.Information("[LinuxInputCapture]   - {Name} ({Path})", device.Name, device.Path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[LinuxInputCapture] Failed to open {Name}", device.Name);
            }
        }
        
        if (_readers.Count == 0)
        {
            var errorMsg = "Failed to open any input devices";
            Log.Error("[LinuxInputCapture] {Error}", errorMsg);
            throw new InvalidOperationException(errorMsg);
        }
        
        _stopRegistration.Dispose();
        _stopRegistration = ct.Register(Stop);
        await Task.CompletedTask;
    }
    
    public void Stop()
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
                catch (Exception ex)
                {
                    Log.Debug(ex, "[LinuxInputCapture] Error unsubscribing from reader events");
                }
            }
            
            Parallel.ForEach(_readers, reader =>
            {
                try
                {
                    reader.Stop();
                    reader.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[LinuxInputCapture] Error stopping reader");
                }
            });
            
            _readers.Clear();
            Log.Information("[LinuxInputCapture] Stopped all readers");
        }
        
        _stopRegistration.Dispose();
    }
    
    private void OnEvdevEventReceived(ILinuxInputReader reader, UInputNative.input_event e)
    {
        var eventType = e.type switch
        {
            UInputNative.EV_KEY => UInputNative.IsMouseButton(e.code) 
                ? InputEventType.MouseButton 
                : InputEventType.Key,
            UInputNative.EV_REL => e.code == UInputNative.REL_WHEEL 
                ? InputEventType.MouseScroll 
                : InputEventType.MouseMove,
            UInputNative.EV_ABS when e.code == UInputNative.ABS_X || e.code == UInputNative.ABS_Y
                => InputEventType.MouseMove,
            UInputNative.EV_SYN => InputEventType.Sync,
            _ => InputEventType.Unknown
        };

        if (!ShouldForwardEvent(eventType))
        {
            return;
        }
        
        var args = new InputCaptureEventArgs
        {
            Type = eventType,
            Code = e.code,
            Value = e.value,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            DeviceName = reader.DeviceName
        };
        
        InputReceived?.Invoke(this, args);
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
            _ => false
        };
    }

    private bool ShouldCaptureDevice(InputDeviceHelper.InputDevice device)
    {
        if (VirtualDeviceConstants.IsCrossMacroVirtualDeviceName(device.Name))
        {
            return false;
        }

        return (_captureMouse && device.IsMouse) || (_captureKeyboard && device.IsKeyboard);
    }
    

    
    private void OnEvdevError(Exception ex)
    {
        Error?.Invoke(this, ex.Message);
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }

    internal interface ILinuxInputReader : IDisposable
    {
        string DeviceName { get; }
        event Action<ILinuxInputReader, UInputNative.input_event>? EventReceived;
        event Action<Exception>? ErrorOccurred;
        void Start();
        void Stop();
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

        public void Stop() => _reader.Stop();

        public void Dispose()
        {
            _reader.EventReceived -= OnReaderEventReceived;
            _reader.ErrorOccurred -= OnReaderErrorOccurred;
            _reader.Dispose();
        }

        private void OnReaderEventReceived(EvdevReader reader, UInputNative.input_event inputEvent)
        {
            EventReceived?.Invoke(this, inputEvent);
        }

        private void OnReaderErrorOccurred(Exception exception)
        {
            ErrorOccurred?.Invoke(exception);
        }
    }
}
