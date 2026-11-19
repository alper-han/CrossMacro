
namespace CrossMacro.Cli;

public abstract class CliCommandHandlerBase<TOptions> : ICliCommandHandler where TOptions : CommandOptions
{
    public Task<OperationResult> ExecuteAsync(CommandOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options is not TOptions typedOptions)
        {
            throw new ArgumentException($"Unsupported command options type: {options.GetType().FullName}", nameof(options));
        }

        return ExecuteAsync(typedOptions, cancellationToken);
    }

    protected abstract Task<OperationResult> ExecuteAsync(TOptions options, CancellationToken cancellationToken);
}
