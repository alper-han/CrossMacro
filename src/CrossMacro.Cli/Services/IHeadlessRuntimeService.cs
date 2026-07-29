
namespace CrossMacro.Cli.Services;

public interface IHeadlessRuntimeService
{
    public Task<HeadlessRuntimeResult> RunAsync(CancellationToken cancellationToken);
}
