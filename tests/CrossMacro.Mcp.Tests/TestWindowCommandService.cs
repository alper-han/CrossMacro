namespace CrossMacro.Mcp.Tests;

internal sealed class TestWindowCommandService : IWindowCommandService
{
    public OperationResult? Result { get; init; }

    public int CallCount { get; private set; }

    public WindowOptions? LastOptions { get; private set; }

    public Task<OperationResult> ExecuteAsync(WindowOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        LastOptions = options;
        return Task.FromResult(Result ?? throw new InvalidOperationException("Window result was not configured."));
    }
}
