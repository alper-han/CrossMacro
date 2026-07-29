
namespace CrossMacro.Platform.Abstractions;

public interface IInputSimulatorPool : IDisposable
{
    public bool HasWarmDevice { get; }
    public Task Completion { get; }
    public Task WarmUpAsync(int screenWidth = 0, int screenHeight = 0);
    public Task<IInputSimulator> AcquireAsync(
        int screenWidth,
        int screenHeight,
        CancellationToken cancellationToken = default);
    public IInputSimulator Acquire(int screenWidth, int screenHeight);
    public void Release(IInputSimulator device, int screenWidth = 0, int screenHeight = 0);
}
