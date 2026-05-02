using CrossMacro.Daemon.Contracts.Ipc;

namespace CrossMacro.Daemon.Services;

/// <summary>
/// Manages the virtual input device (uinput) lifecycle and event simulation.
/// </summary>
public interface IVirtualDeviceManager : IDisposable
{
    /// <summary>
    /// Configures (or re-configures) the virtual device with specific resolution.
    /// If resolution is 0x0, it uses relative mode.
    /// </summary>
    void Configure(int width, int height);

    /// <summary>
    /// Sends a low-level input event to the virtual device.
    /// </summary>
    void SendEvent(ushort type, ushort code, int value);

    /// <summary>
    /// Sends an ordered batch of low-level input events to the virtual device.
    /// </summary>
    void SendEvents(ReadOnlySpan<IpcSimulationRequest> events);
    
    /// <summary>
    /// Resets/Disposes the current uinput device.
    /// </summary>
    void Reset();
}
