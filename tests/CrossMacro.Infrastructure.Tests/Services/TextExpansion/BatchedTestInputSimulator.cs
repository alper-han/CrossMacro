namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

internal class BatchedTestInputSimulator : TestInputSimulator, IBatchedInputSimulator, IAsyncBatchedInputSimulator
{
    public List<InputSimulationStep[]> Batches { get; } = new();

    public bool SupportsBatchedInput => true;

    public void SimulateBatch(ReadOnlySpan<InputSimulationStep> steps)
    {
        Batches.Add(steps.ToArray());
    }

    public Task SimulateBatchAsync(IReadOnlyList<InputSimulationStep> steps, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Batches.Add(steps.ToArray());
        return Task.CompletedTask;
    }
}
