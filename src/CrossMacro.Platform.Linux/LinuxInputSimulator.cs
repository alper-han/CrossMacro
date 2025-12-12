using System;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Native.UInput;
using Serilog;

namespace CrossMacro.Platform.Linux;

public class LinuxInputSimulator : IInputSimulator
{
    private UInputDevice? _device;
    private bool _disposed;
    
    public string ProviderName => "Linux UInput";
    
    public bool IsSupported
    {
        get
        {
            try
            {
                return File.Exists("/dev/uinput") || File.Exists("/dev/input/uinput");
            }
            catch
            {
                return false;
            }
        }
    }
    
    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
        if (_device != null)
        {
            Log.Warning("[LinuxInputSimulator] Already initialized");
            return;
        }
        
        _device = new UInputDevice(screenWidth, screenHeight);
        _device.CreateVirtualInputDevice();
        Log.Information("[LinuxInputSimulator] Initialized with resolution {Width}x{Height}", screenWidth, screenHeight);
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
    
    public void Scroll(int delta)
    {
        _device?.SendEvent(Native.UInput.UInputNative.EV_REL, Native.UInput.UInputNative.REL_WHEEL, delta);
        _device?.SendEvent(Native.UInput.UInputNative.EV_SYN, Native.UInput.UInputNative.SYN_REPORT, 0);
    }
    
    public void KeyPress(int keyCode, bool pressed)
    {
        _device?.EmitKey(keyCode, pressed);
    }
    
    public void Sync()
    {
        _device?.SendEvent(Native.UInput.UInputNative.EV_SYN, Native.UInput.UInputNative.SYN_REPORT, 0);
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _device?.Dispose();
            _device = null;
            _disposed = true;
        }
    }
}
