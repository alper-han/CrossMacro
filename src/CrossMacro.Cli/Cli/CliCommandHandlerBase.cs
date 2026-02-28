using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli;

public abstract class CliCommandHandlerBase<TOptions> : ICliCommandHandler where TOptions : CliCommandOptions
{
    public bool CanHandle(CliCommandOptions options)
    {
        return options is TOptions;
    }

    public Task<CliCommandExecutionResult> ExecuteAsync(CliCommandOptions options, CancellationToken cancellationToken)
    {
        if (options is not TOptions typedOptions)
        {
            throw new ArgumentException($"Unsupported command options type: {options.GetType().FullName}", nameof(options));
        }

        return ExecuteAsync(typedOptions, cancellationToken);
    }

    protected abstract Task<CliCommandExecutionResult> ExecuteAsync(TOptions options, CancellationToken cancellationToken);
}
