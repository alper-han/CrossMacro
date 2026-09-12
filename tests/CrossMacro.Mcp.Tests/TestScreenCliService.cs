namespace CrossMacro.Mcp.Tests;

internal sealed class TestScreenCliService : IScreenCliService
    {
        public CliCommandExecutionResult? Result { get; init; }

        public int CallCount { get; private set; }

        public ScreenCliOptions? LastOptions { get; private set; }

        public Task<CliCommandExecutionResult> ExecuteAsync(ScreenCliOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastOptions = options;
            return Task.FromResult(Result ?? throw new InvalidOperationException("Screen result was not configured."));
        }
    }
