
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowActiveCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "active";

    public string? Validate(string[] parts)
    {
        if (parts.Length is not 4)
        {
            return "Syntax: window active title|class|address|fullscreen|maximize|float|pinned|hidden|geometry $variable";
        }

        if (!WindowActiveFieldSyntax.TryParse(parts[2], out _))
        {
            return $"Unknown field '{parts[2]}'. Expected: title, class, address, fullscreen, maximize, float, pinned, hidden, geometry.";
        }

        if (!IsValidVarName(StripDollar(parts[3])))
        {
            return $"Invalid variable name '{parts[3]}'.";
        }

        return null;
    }

    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        _ = WindowActiveFieldSyntax.TryParse(parts[2], out var field);
        var varName = StripDollar(parts[3]);
        var info = await query.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        var val = field switch
        {
            WindowActiveField.Title => info?.Title ?? string.Empty,
            WindowActiveField.Class => info?.Class ?? string.Empty,
            WindowActiveField.Address => info?.Address ?? string.Empty,
            WindowActiveField.Fullscreen => (info?.IsFullscreen ?? false) ? "true" : "false",
            WindowActiveField.Maximize => (info?.IsMaximized ?? false) ? "true" : "false",
            WindowActiveField.Floating => (info?.IsFloating ?? false) ? "true" : "false",
            WindowActiveField.Pinned => (info?.IsPinned ?? false) ? "true" : "false",
            WindowActiveField.Hidden => (info?.IsHidden ?? false) ? "true" : "false",
            WindowActiveField.Geometry => info != null ? $"{info.X.ToString(CultureInfo.InvariantCulture)} {info.Y.ToString(CultureInfo.InvariantCulture)} {info.Width.ToString(CultureInfo.InvariantCulture)} {info.Height.ToString(CultureInfo.InvariantCulture)}" : string.Empty,
            WindowActiveField.Unknown => string.Empty,
            _ => string.Empty,
        };
        StoreVariable(variables, varName, val, stepNumber);
    }
}
