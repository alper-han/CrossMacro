
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

        var field = parts[2].ToLowerInvariant();
        if (field is not ("title" or "class" or "address" or "fullscreen" or "maximize" or "float" or "pinned" or "hidden" or "geometry"))
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
        var field = parts[2].ToLowerInvariant();
        var varName = StripDollar(parts[3]);
        var info = await query.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        var val = field switch
        {
            "title" => info?.Title ?? string.Empty,
            "class" => info?.Class ?? string.Empty,
            "address" => info?.Address ?? string.Empty,
            "fullscreen" => (info?.IsFullscreen ?? false) ? "true" : "false",
            "maximize" => (info?.IsMaximized ?? false) ? "true" : "false",
            "float" => (info?.IsFloating ?? false) ? "true" : "false",
            "pinned" => (info?.IsPinned ?? false) ? "true" : "false",
            "hidden" => (info?.IsHidden ?? false) ? "true" : "false",
            "geometry" => info != null ? $"{info.X.ToString(CultureInfo.InvariantCulture)} {info.Y.ToString(CultureInfo.InvariantCulture)} {info.Width.ToString(CultureInfo.InvariantCulture)} {info.Height.ToString(CultureInfo.InvariantCulture)}" : string.Empty,
            _ => string.Empty,
        };
        StoreVariable(variables, varName, val, stepNumber);
    }
}
