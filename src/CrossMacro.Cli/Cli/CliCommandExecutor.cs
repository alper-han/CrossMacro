using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli;

public sealed class CliCommandExecutor
{
    private readonly ICliCommandHandlerResolver _handlerResolver;

    public CliCommandExecutor(ICliCommandHandlerResolver handlerResolver)
    {
        _handlerResolver = handlerResolver;
    }

    public async Task<int> ExecuteAsync(CliCommandOptions options, CancellationToken cancellationToken)
    {
        var result = await ExecuteWithResolvedHandlerAsync(options, cancellationToken);
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

        return await handler.ExecuteAsync(options, cancellationToken);
    }
}
