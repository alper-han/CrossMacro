namespace CrossMacro.Mcp.Tests;

internal sealed class TestWindowCliService : IWindowCliService
    {
        public CliCommandExecutionResult? Result { get; init; }

        public int CallCount { get; private set; }

        public WindowCliOptions? LastOptions { get; private set; }

        public Task<CliCommandExecutionResult> ExecuteAsync(WindowCliOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastOptions = options;
            return Task.FromResult(Result ?? throw new InvalidOperationException("Window result was not configured."));
        }
    }
