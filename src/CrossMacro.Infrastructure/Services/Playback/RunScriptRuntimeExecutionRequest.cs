
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunScriptRuntimeExecutionRequest(
    IList<string> scriptSteps,
    IDictionary<string, string> imageAssets,
    double speedMultiplier,
    Func<MacroEvent, CancellationToken, Task> executeEventAsync,
    Func<int, bool, int, int, int> resolveDelayMs)
{
    public IList<string> ScriptSteps { get; } = scriptSteps ?? throw new ArgumentNullException(nameof(scriptSteps));

    public IDictionary<string, string> ImageAssets { get; } = imageAssets ?? throw new ArgumentNullException(nameof(imageAssets));

    public double SpeedMultiplier { get; } = speedMultiplier;

    public Func<MacroEvent, CancellationToken, Task> ExecuteEventAsync { get; } = executeEventAsync ?? throw new ArgumentNullException(nameof(executeEventAsync));

    public Func<int, bool, int, int, int> ResolveDelayMs { get; } = resolveDelayMs ?? throw new ArgumentNullException(nameof(resolveDelayMs));
}
