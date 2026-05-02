using System;
using System.Threading;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Infrastructure.Linux.Native.UInput;
using CrossMacro.Core.Logging;

namespace CrossMacro.Daemon.Services;

public class VirtualDeviceManager : IVirtualDeviceManager
{
    private UInputDevice? _uInputDevice;
    private readonly Lock _lock = new();
    
    public void Configure(int width, int height)
    {
        lock (_lock)
        {
            try 
            {
                if (_uInputDevice != null)
                {
                    _uInputDevice.Dispose();
                    _uInputDevice = null;
                }
                
                _uInputDevice = new UInputDevice(width, height);
                _uInputDevice.CreateVirtualInputDevice();
                Log.Information("[VirtualDeviceManager] Reconfigured UInput device with resolution {W}x{H}", width, height);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[VirtualDeviceManager] Failed to configure UInput device");
                throw;
            }
        }
    }

    public void SendEvent(ushort type, ushort code, int value)
    {
        lock (_lock)
        {
            UInputDevice? device = _uInputDevice;
            if (device == null) return;

            device.SendEvent(type, code, value);
        }
    }

    public void SendEvents(ReadOnlySpan<IpcSimulationRequest> events)
    {
        lock (_lock)
        {
            UInputDevice? device = _uInputDevice;
            if (device == null) return;

            foreach (var inputEvent in events)
            {
                device.SendEvent(inputEvent.Type, inputEvent.Code, inputEvent.Value);
                if (inputEvent.DelayAfterMs > 0)
                {
                    Thread.Sleep(inputEvent.DelayAfterMs);
                }
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            if (_uInputDevice != null)
            {
                _uInputDevice.Dispose();
                _uInputDevice = null;
                Log.Information("[VirtualDeviceManager] Device reset");
            }
        }
    }

    public void Dispose()
    {
        Reset();
    }
}
