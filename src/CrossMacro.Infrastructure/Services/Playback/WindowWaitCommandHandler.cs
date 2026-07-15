using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;
using static CrossMacro.Infrastructure.Services.Playback.WindowCommandHelpers;

namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowWaitCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "wait";
    public string? Validate(string[] parts)
    {
        if (parts.Length < 4) return "Syntax: window wait title|class \"<term>\" [timeout_ms] $variable";
        var field = parts[2].ToLowerInvariant();
        if (field is not ("title" or "class")) return $"Unknown field '{parts[2]}'. Expected: title, class.";
        var varPart = parts[^1];
        var vn = StripDollar(varPart);
        if (!IsValidVarName(vn)) return $"Invalid variable name '{varPart}'.";
        int tVal = 0;
        var hasTimeout = parts.Length > 4 && int.TryParse(parts[^2], NumberStyles.None, CultureInfo.InvariantCulture, out tVal) && tVal > 0;
        var termEndIndex = hasTimeout ? parts.Length - 2 : parts.Length - 1;
        var term = Unquote(string.Join(' ', parts[3..termEndIndex]));
        if (string.IsNullOrWhiteSpace(term)) return "Search term cannot be empty.";
        return null;
    }
    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var field = parts[2].ToLowerInvariant();
        var varName = StripDollar(parts[^1]);
        int tVal = 0;
        var hasTimeout = parts.Length > 4 && int.TryParse(parts[^2], NumberStyles.None, CultureInfo.InvariantCulture, out tVal) && tVal > 0;
        var termEndIndex = hasTimeout ? parts.Length - 2 : parts.Length - 1;
        var timeoutMs = hasTimeout ? tVal : 5000;
        var term = Unquote(string.Join(' ', parts[3..termEndIndex]));
        var deadline = Environment.TickCount64 + timeoutMs;
        WindowInfo? found = null;
        while (Environment.TickCount64 < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windows = await query.GetWindowsAsync(cancellationToken).ConfigureAwait(false);
            found = field is "title" ? FindByTitle(windows, term) : FindByClass(windows, term);
            if (found != null) break;
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }
        StoreVariable(variables, varName, found?.Address ?? string.Empty, stepNumber);
    }
}
