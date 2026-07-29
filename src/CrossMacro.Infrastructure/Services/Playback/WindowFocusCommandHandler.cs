
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowFocusCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "focus";
    public string? Validate(string[] parts)
    {
        if (parts.Length < 3)
        {
            return "Syntax: window focus active|title|class|address <value>";
        }

        var field = parts[2].ToUpperInvariant();
        if (field is "ACTIVE")
        {
            return parts.Length is 3 ? null : "Syntax: window focus active";
        }

        if (field is not ("TITLE" or "CLASS" or "ADDRESS"))
        {
            return $"Unknown field '{parts[2]}'. Expected: active, title, class, address.";
        }

        var term = Unquote(string.Join(' ', parts[3..]));
        if (string.IsNullOrWhiteSpace(term))
        {
            return $"Missing value for 'window focus {field}'.";
        }

        return null;
    }
    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var field = parts[2].ToUpperInvariant();
        if (field is "ACTIVE")
        {
            var info = await query.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
            if (info != null)
            {
                _ = await mutator.FocusWindowByAddressAsync(info.Address, cancellationToken).ConfigureAwait(false);
            }

            return;
        }
        var term = Unquote(string.Join(' ', parts[3..]));
        _ = field switch
        {
            "TITLE" => await mutator.FocusWindowByTitleAsync(term, cancellationToken).ConfigureAwait(false),
            "CLASS" => await mutator.FocusWindowByClassAsync(term, cancellationToken).ConfigureAwait(false),
            "ADDRESS" => await mutator.FocusWindowByAddressAsync(term, cancellationToken).ConfigureAwait(false),
            _ => false,
        };
    }

}
