
namespace CrossMacro.Platform.Abstractions.Input.Simulation;

public interface IInputSimulatorPool : IDisposable
{
    public bool HasWarmDevice { get; }
    public Task WarmUpAsync(
        int screenWidth = 0,
        int screenHeight = 0,
        CancellationToken cancellationToken = default);
    public Task<IInputSimulator> AcquireAsync(
        int screenWidth,
        int screenHeight,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a device leased by <see cref="AcquireAsync"/>; the pool keeps it warm or disposes it.</summary>
    public void Release(IInputSimulator device);
}
