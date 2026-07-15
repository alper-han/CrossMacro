
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

    Task ConfigureAsync(int width, int height, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a low-level input event to the virtual device.
    /// </summary>
    void SendEvent(ushort type, ushort code, int value);

    Task SendEventAsync(ushort type, ushort code, int value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an ordered batch of low-level input events to the virtual device.
    /// </summary>
    void SendEvents(ReadOnlySpan<IpcSimulationRequest> events);

    Task SendEventsAsync(IReadOnlyList<IpcSimulationRequest> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets/Disposes the current uinput device.
    /// </summary>
    void Reset();

    Task ResetAsync(CancellationToken cancellationToken = default);
}
