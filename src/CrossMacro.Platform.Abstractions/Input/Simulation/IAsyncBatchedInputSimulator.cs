namespace CrossMacro.Platform.Abstractions.Input.Simulation;

public interface IAsyncBatchedInputSimulator
{
    public Task SimulateBatchAsync(
        IReadOnlyList<InputSimulationStep> steps,
        CancellationToken cancellationToken = default);
}
