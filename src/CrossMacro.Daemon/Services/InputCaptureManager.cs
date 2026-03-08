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
    private readonly List<EvdevReader> _readers = new();
    private readonly Lock _lock = new();

    public CaptureStartResult StartCapture(bool captureMouse, bool captureKeyboard, Action<UInputNative.input_event> onEvent)
    {
        lock (_lock)
        {
            StopCapture(); // Clear existing

            var devices = InputDeviceHelper.GetAvailableDevices();
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
                    var evReader = new EvdevReader(dev.Path, dev.Name);
                    evReader.EventReceived += (sender, e) => 
                    {
                        // Invoke callback. 
                        // Note: This runs on EvdevReader's thread.
                        // Callback must handle synchronization.
                        try 
                        {
                            onEvent(e);
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
}
