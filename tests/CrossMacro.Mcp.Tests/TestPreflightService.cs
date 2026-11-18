using CrossMacro.Commands.Runtime;

namespace CrossMacro.Mcp.Tests;

internal sealed class TestPreflightService : IPreflightService
{
    public OperationResult Result { get; init; } = OperationResult.Ok("Preflight checks passed.");

    public List<PreflightTarget> Targets { get; } = [];

    public Task<OperationResult> CheckAsync(PreflightTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Targets.Add(target);
        return Task.FromResult(Result);
    }
}
