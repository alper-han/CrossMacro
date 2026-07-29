
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

        var field = parts[2].ToUpperInvariant();
        if (field is not ("TITLE" or "CLASS" or "ADDRESS" or "FULLSCREEN" or "MAXIMIZE" or "FLOAT" or "PINNED" or "HIDDEN" or "GEOMETRY"))
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
        var field = parts[2].ToUpperInvariant();
        var varName = StripDollar(parts[3]);
        var info = await query.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        var val = field switch
        {
            "TITLE" => info?.Title ?? string.Empty,
            "CLASS" => info?.Class ?? string.Empty,
            "ADDRESS" => info?.Address ?? string.Empty,
            "FULLSCREEN" => (info?.IsFullscreen ?? false) ? "true" : "false",
            "MAXIMIZE" => (info?.IsMaximized ?? false) ? "true" : "false",
            "FLOAT" => (info?.IsFloating ?? false) ? "true" : "false",
            "PINNED" => (info?.IsPinned ?? false) ? "true" : "false",
            "HIDDEN" => (info?.IsHidden ?? false) ? "true" : "false",
            "GEOMETRY" => info != null ? $"{info.X.ToString(CultureInfo.InvariantCulture)} {info.Y.ToString(CultureInfo.InvariantCulture)} {info.Width.ToString(CultureInfo.InvariantCulture)} {info.Height.ToString(CultureInfo.InvariantCulture)}" : string.Empty,
            _ => string.Empty,
        };
        StoreVariable(variables, varName, val, stepNumber);
    }
}
