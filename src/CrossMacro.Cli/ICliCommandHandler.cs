
namespace CrossMacro.Cli;

public interface ICliCommandHandler
{
    public Task<OperationResult> ExecuteAsync(CommandOptions options, CancellationToken cancellationToken);
}
