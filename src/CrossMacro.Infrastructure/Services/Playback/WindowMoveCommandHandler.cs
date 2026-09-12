
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowMoveCommandHandler(
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null) : IWindowCommandHandler
{
    private readonly Func<TimeSpan, CancellationToken, Task>? _delayAsync = delayAsync;

    public string SubCommand => "move";
    public string? Validate(string[] parts)
    {
        if (parts.Length is not 4)
        {
            return "Syntax: window move <x> <y>";
        }

        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return $"'window move' requires integer coordinates. Got '{parts[2]}' '{parts[3]}'.";
        }

        return null;
    }
    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var x = int.Parse(parts[2], CultureInfo.InvariantCulture);
        var y = int.Parse(parts[3], CultureInfo.InvariantCulture);
        await WindowGeometryUnlocker.UnlockAsync(query, mutator, cancellationToken, _delayAsync).ConfigureAwait(false);
        _ = await mutator.MoveActiveWindowAsync(x, y, cancellationToken).ConfigureAwait(false);
    }
}
