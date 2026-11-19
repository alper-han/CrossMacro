namespace CrossMacro.Mcp.Tests;

internal sealed class TestCliCommandHandler<TOptions>(OperationResult result) : CliCommandHandlerBase<TOptions>
    where TOptions : CommandOptions
{
    private readonly OperationResult _result = result;

    public TOptions? LastOptions { get; private set; }

    protected override Task<OperationResult> ExecuteAsync(TOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastOptions = options;
        return Task.FromResult(_result);
    }
}
