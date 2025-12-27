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
    private readonly List<EvdevReader> _readers = new();
    private bool _disposed;
    private CancellationTokenSource? _cts;
    
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
    
    public void Configure(bool captureMouse, bool captureKeyboard)
    {
        _captureMouse = captureMouse;
        _captureKeyboard = captureKeyboard;
        Log.Information("[LinuxInputCapture] Configured: Mouse={Mouse}, Keyboard={Keyboard}", captureMouse, captureKeyboard);
    }
    
    public IReadOnlyList<InputDeviceInfo> GetAvailableDevices()
    {
        var nativeDevices = InputDeviceHelper.GetAvailableDevices();
        return nativeDevices.Select(d => new InputDeviceInfo
        {
            Path = d.Path,
            Name = d.Name,
            IsMouse = d.IsMouse,
            IsKeyboard = d.IsKeyboard,
            IsTouchpad = false 
        }).ToList();
    }
    
    public async Task StartAsync(CancellationToken ct)
    {
        if (_readers.Count > 0)
        {
            Log.Warning("[LinuxInputCapture] Already started");
            return;
        }
        
        var nativeDevices = InputDeviceHelper.GetAvailableDevices();
        
        var devicesToUse = nativeDevices.Where(d => 
            (_captureMouse && d.IsMouse) || 
            (_captureKeyboard && d.IsKeyboard)
        ).ToList();
        
        if (devicesToUse.Count == 0)
        {
            var errorMsg = "No matching input devices found";
            Log.Error("[LinuxInputCapture] {Error}", errorMsg);
            Error?.Invoke(this, errorMsg);
            return;
        }
        
        Log.Information("[LinuxInputCapture] Starting capture on {Count} device(s):", devicesToUse.Count);
        
        foreach (var device in devicesToUse)
        {
            try
            {
                var reader = new EvdevReader(device.Path, device.Name);
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
            Error?.Invoke(this, errorMsg);
            return;
        }
        
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        
        await Task.Run(() =>
        {
            try
            {
                _cts.Token.WaitHandle.WaitOne();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[LinuxInputCapture] Error during capture");
                Error?.Invoke(this, ex.Message);
            }
        }, ct);
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
        
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
    
    private void OnEvdevEventReceived(EvdevReader reader, UInputNative.input_event e)
    {
        var eventType = e.type switch
        {
            UInputNative.EV_KEY => IsMouseButtonCode(e.code) 
                ? InputEventType.MouseButton 
                : InputEventType.Key,
            UInputNative.EV_REL => e.code == UInputNative.REL_WHEEL 
                ? InputEventType.MouseScroll 
                : InputEventType.MouseMove,
            UInputNative.EV_SYN => InputEventType.Sync,
            _ => InputEventType.Sync
        };
        
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
    
    /// <summary>
    /// Check if evdev code is a mouse button (BTN_LEFT=272 through BTN_TASK=279)
    /// </summary>
    private static bool IsMouseButtonCode(ushort code)
    {
        // BTN_LEFT (0x110=272) through BTN_TASK (0x117=279)
        return code >= 272 && code <= 279;
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
}
