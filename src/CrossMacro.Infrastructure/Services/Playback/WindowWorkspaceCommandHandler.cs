
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowWorkspaceCommandHandler(string cmd) : IWindowCommandHandler
{
    private readonly WindowCommandMode _mode = cmd switch
    {
        "getdesktop" => WindowCommandMode.WorkspaceGet,
        "setdesktop" => WindowCommandMode.WorkspaceSwitch,
        "setdesktopforwindow" => WindowCommandMode.WorkspaceMoveWindow,
        _ => throw new ArgumentException("Unknown workspace command.", nameof(cmd)),
    };

    public string SubCommand { get; } = cmd;
    public string? Validate(string[] parts)
    {
        if (_mode is WindowCommandMode.WorkspaceGet)
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
        else if (_mode is WindowCommandMode.WorkspaceSwitch)
        {
            if (parts.Length < 3)
            {
                return "Syntax: window setdesktop <workspace>";
            }
        }
        else if (_mode is WindowCommandMode.WorkspaceMoveWindow)
        {
            if (parts.Length < 4)
            {
                return "Syntax: window setdesktopforwindow active|address <addr> <workspace>";
            }

            var field = parts[2].ToUpperInvariant();
            if (field is "ACTIVE")
            {
                return parts.Length >= 4 ? null : "Syntax: window setdesktopforwindow active <workspace>";
            }

            if (field is "ADDRESS")
            {
                return parts.Length >= 5 ? null : "Syntax: window setdesktopforwindow address <addr> <workspace>";
            }

            return $"Unknown field '{parts[2]}'. Expected: active, address.";
        }
        return null;
    }
    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        if (_mode is WindowCommandMode.WorkspaceGet)
        {
            var ws = await workspace.GetActiveWorkspaceAsync(cancellationToken).ConfigureAwait(false);
            StoreVariable(variables, StripDollar(parts[2]), ws ?? string.Empty, stepNumber);
        }
        else if (_mode is WindowCommandMode.WorkspaceSwitch)
        {
            _ = await workspace.SwitchWorkspaceAsync(Unquote(string.Join(' ', parts[2..])), cancellationToken).ConfigureAwait(false);
        }
        else if (_mode is WindowCommandMode.WorkspaceMoveWindow)
        {
            var field = parts[2].ToUpperInvariant();
            if (field is "ACTIVE")
            {
                _ = await workspace.MoveActiveWindowToWorkspaceAsync(Unquote(string.Join(' ', parts[3..])), cancellationToken).ConfigureAwait(false);
            }
            else if (field is "ADDRESS")
            {
                _ = await workspace.MoveWindowToWorkspaceByAddressAsync(parts[3], Unquote(string.Join(' ', parts[4..])), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
