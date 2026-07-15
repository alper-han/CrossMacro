
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowWorkspaceCommandHandler : IWindowCommandHandler
{
    private readonly string _cmd;
    public WindowWorkspaceCommandHandler(string cmd) => _cmd = cmd;
    public string SubCommand => _cmd;
    public string? Validate(string[] parts)
    {
        if (_cmd is "getdesktop")
        {
            if (parts.Length is not 3)
            {
                return "Syntax: window getdesktop $variable";
            }

            if (!IsValidVarName(StripDollar(parts[2])))
            {
                return $"Invalid variable name '{parts[2]}'.";
            }
        }
        else if (_cmd is "setdesktop")
        {
            if (parts.Length < 3)
            {
                return "Syntax: window setdesktop <workspace>";
            }
        }
        else if (_cmd is "setdesktopforwindow")
        {
            if (parts.Length < 4)
            {
                return "Syntax: window setdesktopforwindow active|address <addr> <workspace>";
            }

            var field = parts[2].ToLowerInvariant();
            if (field is "active")
            {
                return parts.Length >= 4 ? null : "Syntax: window setdesktopforwindow active <workspace>";
            }

            if (field is "address")
            {
                return parts.Length >= 5 ? null : "Syntax: window setdesktopforwindow address <addr> <workspace>";
            }

            return $"Unknown field '{parts[2]}'. Expected: active, address.";
        }
        return null;
    }
    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        if (_cmd is "getdesktop")
        {
            var ws = await workspace.GetActiveWorkspaceAsync(cancellationToken).ConfigureAwait(false);
            StoreVariable(variables, StripDollar(parts[2]), ws ?? string.Empty, stepNumber);
        }
        else if (_cmd is "setdesktop")
        {
            await workspace.SwitchWorkspaceAsync(Unquote(string.Join(' ', parts[2..])), cancellationToken).ConfigureAwait(false);
        }
        else if (_cmd is "setdesktopforwindow")
        {
            var field = parts[2].ToLowerInvariant();
            if (field is "active")
            {
                await workspace.MoveActiveWindowToWorkspaceAsync(Unquote(string.Join(' ', parts[3..])), cancellationToken).ConfigureAwait(false);
            }
            else if (field is "address")
            {
                await workspace.MoveWindowToWorkspaceByAddressAsync(parts[3], Unquote(string.Join(' ', parts[4..])), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
