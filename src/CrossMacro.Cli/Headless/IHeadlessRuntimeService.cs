
namespace CrossMacro.Cli.Headless;

public interface IHeadlessRuntimeService
{
    public Task<OperationResult> RunAsync(CancellationToken cancellationToken);
}
