namespace CrossMacro.Mcp.Tests;

internal sealed class RecordingCliCommandHandler : ICliCommandHandler
{
    public CliCommandOptions? LastOptions { get; private set; }

    public Task<CliCommandExecutionResult> ExecuteAsync(CliCommandOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastOptions = options;
        return Task.FromResult(CliCommandExecutionResult.Ok("Compatibility command completed."));
    }
}
