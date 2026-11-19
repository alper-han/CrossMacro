namespace CrossMacro.Mcp.Tests;

internal sealed class TestRunScriptExecutionService : IRunScriptExecutionService
{
    public OperationResult? Result { get; init; }

    public RunScriptExecutionRequest? LastRequest { get; private set; }

    public Task<OperationResult> ExecuteAsync(RunScriptExecutionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastRequest = request;
        return Task.FromResult(Result ?? throw new InvalidOperationException("Run result was not configured."));
    }
}
