using System;
using System.Collections.Generic;
using System.Linq;
using CrossMacro.Platform.Linux.Native;
using CrossMacro.Platform.Linux.Native.UInput;
using CrossMacro.Platform.Linux.Native.Evdev;
using Serilog;

namespace CrossMacro.Daemon.Services;

public class InputCaptureManager : IInputCaptureManager
{
    private readonly List<ILinuxCaptureReader> _readers = new();
    private readonly Lock _lock = new();
    private readonly Func<IReadOnlyList<InputDeviceHelper.InputDevice>> _deviceEnumerator;
    private readonly Func<InputDeviceHelper.InputDevice, ILinuxCaptureReader> _readerFactory;

    public InputCaptureManager()
        : this(
            () => InputDeviceHelper.GetAvailableDevices(),
            device => new EvdevCaptureReaderAdapter(new EvdevReader(device.Path, device.Name)))
    {
    }

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
            var targetDevices = devices.Where(d => (captureMouse && d.IsMouse) || (captureKeyboard && d.IsKeyboard)).ToList();
            
            Log.Information("[InputCaptureManager] Starting capture on {Count} devices", targetDevices.Count);

            if (targetDevices.Count == 0)
            {
                return CaptureStartResult.Failed("No matching input devices found.");
            }

            foreach (var dev in targetDevices)
            {
                try 
                {
                    var evReader = _readerFactory(dev);
                    evReader.EventReceived += (sender, e) => 
                    {
                        // Invoke callback. 
                        // Note: This runs on EvdevReader's thread.
                        // Callback must handle synchronization.
                        try 
                        {
                            if (ShouldForwardEvent(e, captureMouse, captureKeyboard))
                            {
                                onEvent(e);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Verbose(ex, "[InputCaptureManager] Error in event callback");
                        }
                    };
                    evReader.Start();
                    _readers.Add(evReader);
                }
                catch (Exception ex)
                {
                    Log.Warning("Failed to open {Path}: {Msg}", dev.Path, ex.Message);
                }
            }

            if (_readers.Count == 0)
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

    private static bool ShouldForwardEvent(UInputNative.input_event inputEvent, bool captureMouse, bool captureKeyboard)
    {
        return inputEvent.type switch
        {
            UInputNative.EV_KEY when UInputNative.IsMouseButton(inputEvent.code) => captureMouse,
            UInputNative.EV_KEY => captureKeyboard,
            UInputNative.EV_REL => captureMouse,
            UInputNative.EV_SYN => captureMouse,
            _ => false
        };
    }

    internal interface ILinuxCaptureReader : IDisposable
    {
        event Action<ILinuxCaptureReader, UInputNative.input_event>? EventReceived;
        void Start();
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

        private void OnReaderEventReceived(EvdevReader reader, UInputNative.input_event inputEvent)
        {
            EventReceived?.Invoke(this, inputEvent);
        }
    }
}
