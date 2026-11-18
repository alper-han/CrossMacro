
namespace CrossMacro.Cli;

public sealed class CliCommandExecutor(ICliCommandHandlerResolver handlerResolver)
{
    private readonly ICliCommandHandlerResolver _handlerResolver = handlerResolver;

    public async Task<int> ExecuteAsync(CommandOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = await ExecuteResultAsync(options, cancellationToken).ConfigureAwait(false);
        CliOutputFormatter.Write(result, options.JsonOutput);
        return (int)result.Outcome;
    }

    public async Task<OperationResult> ExecuteResultAsync(CommandOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var handler = _handlerResolver.Resolve(options);
        if (handler is null)
        {
            return OperationResult.Fail(
                ExecutionOutcomeCode.InvalidArguments,
                $"No handler registered for command options type: {options.GetType().Name}");
        }

        return await handler.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
    }
}
