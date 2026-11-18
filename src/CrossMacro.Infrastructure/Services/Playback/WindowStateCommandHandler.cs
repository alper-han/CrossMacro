
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowStateCommandHandler(
    string state,
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null) : IWindowCommandHandler
{
    private readonly Func<TimeSpan, CancellationToken, Task>? _delayAsync = delayAsync;

    public string SubCommand { get; } = state;
    public string? Validate(string[] parts)
    {
        if (parts.Length >= 3 && !parts[2].Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            return $"Syntax: window {SubCommand} [active]";
        }

        return null;
    }
    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowManager manager, CancellationToken cancellationToken)
    {
        _ = SubCommand switch
        {
            "fullscreen" => await manager.FullscreenActiveWindowAsync(cancellationToken).ConfigureAwait(false),
            "maximize" => await manager.MaximizeActiveWindowAsync(cancellationToken).ConfigureAwait(false),
            "float" => await manager.FloatActiveWindowAsync(cancellationToken).ConfigureAwait(false),
            "center" => await UnlockAndCenterAsync(manager, _delayAsync, cancellationToken).ConfigureAwait(false),
            _ => false,
        };
    }
    private static async Task<bool> UnlockAndCenterAsync(
        IWindowManager manager,
        Func<TimeSpan, CancellationToken, Task>? delayAsync,
        CancellationToken cancellationToken)
    {
        await WindowGeometryUnlocker.UnlockAsync(manager, cancellationToken, delayAsync).ConfigureAwait(false);
        return await manager.CenterActiveWindowAsync(cancellationToken).ConfigureAwait(false);
    }
}
