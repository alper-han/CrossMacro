using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowResizeCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "resize";
    public string? Validate(string[] parts)
    {
        if (parts.Length is not 4) return "Syntax: window resize <width> <height>";
        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h))
            return $"'window resize' requires integer dimensions. Got '{parts[2]}' '{parts[3]}'.";
        if (w <= 0 || h <= 0) return $"'window resize' dimensions must be positive. Got {w}x{h}.";
        return null;
    }
    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var w = int.Parse(parts[2], CultureInfo.InvariantCulture);
        var h = int.Parse(parts[3], CultureInfo.InvariantCulture);
        await WindowGeometryUnlocker.UnlockAsync(query, mutator, cancellationToken).ConfigureAwait(false);
        await mutator.ResizeActiveWindowAsync(w, h, cancellationToken).ConfigureAwait(false);
    }
}
