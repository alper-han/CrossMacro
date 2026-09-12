namespace CrossMacro.Mcp.Tests;

internal sealed class TestRunScriptExecutionService : IRunScriptExecutionService
    {
        public MacroExecutionResult? Result { get; init; }

        public RunCliExecutionRequest? LastRequest { get; private set; }

        public Task<MacroExecutionResult> ExecuteAsync(RunCliExecutionRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(Result ?? throw new InvalidOperationException("Run result was not configured."));
        }
    }
