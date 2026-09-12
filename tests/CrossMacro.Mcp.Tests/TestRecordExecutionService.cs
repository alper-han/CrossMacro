namespace CrossMacro.Mcp.Tests;

internal sealed class TestRecordExecutionService : IRecordExecutionService
    {
        public RecordExecutionResult? Result { get; init; }

        public RecordExecutionRequest? LastRequest { get; private set; }

        public Task<RecordExecutionResult> ExecuteAsync(RecordExecutionRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(Result ?? throw new InvalidOperationException("Record result was not configured."));
        }
    }
