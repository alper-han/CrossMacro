namespace CrossMacro.Platform.Abstractions;

public interface IAsyncBatchedInputSimulator
{
    public Task SimulateBatchAsync(
        IReadOnlyList<InputSimulationStep> steps,
        CancellationToken cancellationToken = default);
}
