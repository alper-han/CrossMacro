
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Coordinates runtime-script execution without taking ownership of playback resources.
/// </summary>
internal sealed class RunScriptRuntimeCoordinator
{
    private readonly RunScriptRuntimeExecutor _executor;

    public RunScriptRuntimeCoordinator(RunScriptRuntimeExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public Task ExecuteAsync(RunScriptRuntimeExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _executor.ExecuteAsync(request, cancellationToken);
    }
}
