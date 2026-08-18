
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowStateCommandHandler(string state) : IWindowCommandHandler
{
    public string SubCommand { get; } = state;
    public string? Validate(string[] parts)
    {
        if (parts.Length >= 3 && !parts[2].Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            return $"Syntax: window {SubCommand} [active]";
        }

        return null;
    }
    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        _ = SubCommand switch
        {
            "fullscreen" => await mutator.FullscreenActiveWindowAsync(cancellationToken).ConfigureAwait(false),
            "maximize" => await mutator.MaximizeActiveWindowAsync(cancellationToken).ConfigureAwait(false),
            "float" => await mutator.FloatActiveWindowAsync(cancellationToken).ConfigureAwait(false),
            "center" => await UnlockAndCenterAsync(query, mutator, cancellationToken).ConfigureAwait(false),
            _ => false,
        };
    }
    private static async Task<bool> UnlockAndCenterAsync(IWindowQueryService query, IWindowMutationService mutator, CancellationToken cancellationToken)
    {
        await WindowGeometryUnlocker.UnlockAsync(query, mutator, cancellationToken).ConfigureAwait(false);
        return await mutator.CenterActiveWindowAsync(cancellationToken).ConfigureAwait(false);
    }
}
