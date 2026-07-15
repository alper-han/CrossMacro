using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;
using static CrossMacro.Infrastructure.Services.Playback.WindowCommandHelpers;

namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowCloseCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "close";
    public string? Validate(string[] parts)
    {
        if (parts.Length < 3) return "Syntax: window close active|title|address <value>";
        var field = parts[2].ToLowerInvariant();
        if (field is "active") return parts.Length is 3 ? null : "Syntax: window close active";
        if (field is not ("title" or "address")) return $"Unknown field '{parts[2]}'. Expected: active, title, address.";
        var term = Unquote(string.Join(' ', parts[3..]));
        if (string.IsNullOrWhiteSpace(term)) return $"Missing value for 'window close {field}'.";
        return null;
    }
    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var field = parts[2].ToLowerInvariant();
        if (field is "active")
        {
            var info = await query.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
            if (info != null) await mutator.CloseWindowByAddressAsync(info.Address, cancellationToken).ConfigureAwait(false);
            return;
        }
        var term = Unquote(string.Join(' ', parts[3..]));
        _ = field switch {
            "title" => await mutator.CloseWindowByTitleAsync(term, cancellationToken).ConfigureAwait(false),
            "address" => await mutator.CloseWindowByAddressAsync(term, cancellationToken).ConfigureAwait(false),
            _ => false,
        };
    }

    private static async Task<bool> UnlockAndCenterAsync(IWindowQueryService query, IWindowMutationService mutator, CancellationToken cancellationToken)
    {
        await WindowGeometryUnlocker.UnlockAsync(query, mutator, cancellationToken).ConfigureAwait(false);
        return await mutator.CenterActiveWindowAsync(cancellationToken).ConfigureAwait(false);
    }
}
