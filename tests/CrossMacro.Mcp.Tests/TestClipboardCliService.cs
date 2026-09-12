namespace CrossMacro.Mcp.Tests;

internal sealed class TestClipboardCliService : IClipboardCliService
    {
        public CliCommandExecutionResult? GetResult { get; init; }

        public CliCommandExecutionResult? SetResult { get; init; }

        public int GetCallCount { get; private set; }

        public int SetCallCount { get; private set; }

        public string? LastSetText { get; private set; }

        public Task<CliCommandExecutionResult> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetCallCount++;
            return Task.FromResult(GetResult ?? throw new InvalidOperationException("Get result was not configured."));
        }

        public Task<CliCommandExecutionResult> SetTextAsync(string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetCallCount++;
            LastSetText = text;
            return Task.FromResult(SetResult ?? throw new InvalidOperationException("Set result was not configured."));
        }

        public Task<CliCommandExecutionResult> SetFileAsync(string filePath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<CliCommandExecutionResult> ClearAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
