namespace CrossMacro.Mcp.Tests;

internal sealed class TestCliCommandHandler<TOptions>(CliCommandExecutionResult result) : CliCommandHandlerBase<TOptions>
    where TOptions : CliCommandOptions
{
    private readonly CliCommandExecutionResult _result = result;

    public TOptions? LastOptions { get; private set; }

    protected override Task<CliCommandExecutionResult> ExecuteAsync(TOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastOptions = options;
        return Task.FromResult(_result);
    }
}
