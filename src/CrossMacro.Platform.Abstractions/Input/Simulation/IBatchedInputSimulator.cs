
namespace CrossMacro.Platform.Abstractions.Input.Simulation;

public interface IBatchedInputSimulator
{
    public bool SupportsBatchedInput { get; }

    public void SimulateBatch(ReadOnlySpan<InputSimulationStep> steps);
}
