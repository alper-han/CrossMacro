
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Owns event iteration while the façade supplies the event policy and execution boundary.
/// </summary>
internal static class MacroPlaybackEventCoordinator
{
    public static async Task ExecuteAsync(
        MacroSequence macro,
        Func<MacroEvent, CancellationToken, Task> executeEventAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(macro);
        ArgumentNullException.ThrowIfNull(executeEventAsync);

        foreach (var ev in macro.Events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await executeEventAsync(ev, cancellationToken).ConfigureAwait(false);
        }
    }
}
