
namespace CrossMacro.Cli.Services;

public interface IHeadlessRuntimeService
{
    Task<HeadlessRuntimeResult> RunAsync(CancellationToken cancellationToken);
}
