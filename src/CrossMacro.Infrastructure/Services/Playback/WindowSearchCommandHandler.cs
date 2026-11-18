
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowSearchCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "search";

    public int OutputVariableIndex(string[] parts) => parts.Length - 1;

    public string? Validate(string[] parts)
    {
        if (parts.Length < 4)
        {
            return "Syntax: window search title|class \"<term>\" <var>";
        }

        var field = parts[2].ToUpperInvariant();
        if (field is not ("TITLE" or "CLASS"))
        {
            return $"Unknown field '{parts[2]}'. Expected: title, class.";
        }

        var varPart = parts[^1];
        if (!TryGetOutputVariableName(varPart, out _))
        {
            return $"Invalid variable name '{varPart}'.";
        }

        var term = Unquote(string.Join(' ', parts[3..^1]));
        if (string.IsNullOrWhiteSpace(term))
        {
            return "Search term cannot be empty.";
        }

        return null;
    }

    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowManager manager, CancellationToken cancellationToken)
    {
        var field = parts[2].ToUpperInvariant();
        var varName = parts[^1];
        var term = Unquote(string.Join(' ', parts[3..^1]));
        var windows = await manager.GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        var match = field is "TITLE" ? FindByTitle(windows, term) : FindByClass(windows, term);
        StoreVariable(variables, varName, match?.Address ?? string.Empty, stepNumber);
    }
}
