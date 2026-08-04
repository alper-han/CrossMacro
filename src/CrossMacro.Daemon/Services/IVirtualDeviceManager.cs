
namespace CrossMacro.Daemon.Services;

/// <summary>
/// Manages the virtual input device (uinput) lifecycle and event simulation.
/// </summary>
internal interface IVirtualDeviceManager : IDisposable
{
    /// <summary>
    /// Creates the default relative device when no virtual device exists yet.
    /// An already configured device is preserved.
    /// </summary>
    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) =>
        ConfigureAsync(0, 0, cancellationToken);

    /// <summary>
    /// Configures (or re-configures) the virtual device with specific resolution.
    /// If resolution is 0x0, it uses relative mode.
    /// </summary>
    public Task ConfigureAsync(int width, int height, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a low-level input event to the virtual device.
    /// </summary>
    public Task SendEventAsync(ushort type, ushort code, int value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an ordered batch of low-level input events to the virtual device.
    /// </summary>
    public Task SendEventsAsync(IReadOnlyList<IpcSimulationRequest> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets/Disposes the current uinput device.
    /// </summary>
    public Task ResetAsync(CancellationToken cancellationToken = default);
}
