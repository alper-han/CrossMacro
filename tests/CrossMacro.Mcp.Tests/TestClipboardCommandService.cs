namespace CrossMacro.Mcp.Tests;

internal sealed class TestClipboardCommandService : IClipboardCommandService
{
    public OperationResult? GetResult { get; init; }

    public OperationResult? SetResult { get; init; }

    public int GetCallCount { get; private set; }

    public int SetCallCount { get; private set; }

    public string? LastSetText { get; private set; }

    public Task<OperationResult> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GetCallCount++;
        return Task.FromResult(GetResult ?? throw new InvalidOperationException("Get result was not configured."));
    }

    public Task<OperationResult> SetTextAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetCallCount++;
        LastSetText = text;
        return Task.FromResult(SetResult ?? throw new InvalidOperationException("Set result was not configured."));
    }

    public Task<OperationResult> SetFileAsync(string filePath, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<OperationResult> ClearAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}
