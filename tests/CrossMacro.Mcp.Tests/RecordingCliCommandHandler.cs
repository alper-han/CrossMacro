namespace CrossMacro.Mcp.Tests;

internal sealed class RecordingCliCommandHandler : ICliCommandHandler
{
    public CommandOptions? LastOptions { get; private set; }

    public Task<OperationResult> ExecuteAsync(CommandOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastOptions = options;
        return Task.FromResult(OperationResult.Ok("Compatibility command completed."));
    }
}
