
namespace CrossMacro.Platform.Abstractions;

public interface IBatchedInputSimulator
{
    bool SupportsBatchedInput { get; }

    void SimulateBatch(ReadOnlySpan<InputSimulationStep> steps);
}
