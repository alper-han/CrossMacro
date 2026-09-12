namespace CrossMacro.Mcp.Tests;

internal sealed class TestCliPreflightService : ICliPreflightService
    {
        public CliPreflightResult Result { get; init; } = CliPreflightResult.Ok();

        public List<CliPreflightTarget> Targets { get; } = [];

        public Task<CliPreflightResult> CheckAsync(CliPreflightTarget target, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Targets.Add(target);
            return Task.FromResult(Result);
        }
    }
