
namespace CrossMacro.Cli.Services.Runtime;

public interface IHeadlessRuntimeService
{
    public Task<HeadlessRuntimeResult> RunAsync(CancellationToken cancellationToken);
}
