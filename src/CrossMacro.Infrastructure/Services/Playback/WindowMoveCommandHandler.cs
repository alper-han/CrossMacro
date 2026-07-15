using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowMoveCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "move";
    public string? Validate(string[] parts)
    {
        if (parts.Length is not 4) return "Syntax: window move <x> <y>";
        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            return $"'window move' requires integer coordinates. Got '{parts[2]}' '{parts[3]}'.";
        return null;
    }
    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var x = int.Parse(parts[2], CultureInfo.InvariantCulture);
        var y = int.Parse(parts[3], CultureInfo.InvariantCulture);
        await WindowGeometryUnlocker.UnlockAsync(query, mutator, cancellationToken).ConfigureAwait(false);
        await mutator.MoveActiveWindowAsync(x, y, cancellationToken).ConfigureAwait(false);
    }
}
