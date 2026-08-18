
namespace CrossMacro.Cli;

public sealed class CliCommandExecutor(ICliCommandHandlerResolver handlerResolver)
{
    private readonly ICliCommandHandlerResolver _handlerResolver = handlerResolver;

    public async Task<int> ExecuteAsync(CliCommandOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = await ExecuteWithResolvedHandlerAsync(options, cancellationToken).ConfigureAwait(false);
        CliOutputFormatter.Write(result, options.JsonOutput);
        return result.ExitCode;
    }

    private async Task<CliCommandExecutionResult> ExecuteWithResolvedHandlerAsync(CliCommandOptions options, CancellationToken cancellationToken)
    {
        var handler = _handlerResolver.Resolve(options);
        if (handler is null)
        {
            return CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                $"No handler registered for command options type: {options.GetType().Name}");
        }

        return await handler.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
    }
}
