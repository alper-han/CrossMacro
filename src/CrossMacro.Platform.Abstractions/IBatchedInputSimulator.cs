
namespace CrossMacro.Platform.Abstractions;

public interface IBatchedInputSimulator
{
    public bool SupportsBatchedInput { get; }

    public void SimulateBatch(ReadOnlySpan<InputSimulationStep> steps);
}
