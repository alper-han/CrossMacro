
namespace CrossMacro.Platform.Abstractions;

public interface IInputSimulatorPool : IDisposable
{
    bool HasWarmDevice { get; }
    Task Completion { get; }
    Task WarmUpAsync(int screenWidth = 0, int screenHeight = 0);
    IInputSimulator Acquire(int screenWidth, int screenHeight);
    void Release(IInputSimulator device, int screenWidth = 0, int screenHeight = 0);
}
