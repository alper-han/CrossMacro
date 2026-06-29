using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Playback;

internal interface IWindowCommandHandler
{
    string SubCommand { get; }
    string? Validate(string[] parts);
    Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService queryService, IWindowMutationService mutationService, IWorkspaceManagementService workspaceService, CancellationToken cancellationToken);
}

internal static class WindowCommandHelpers
{
    public static bool TryExtractTermAndVar(string input, out string? term, out string? varName, out string? error)
    {
        term = null;
        varName = null;
        error = null;

        input = input.Trim();
        var lastSpace = input.LastIndexOf(' ');
        if (lastSpace < 0)
        {
            error = "Syntax: window search title|class \"<term>\" $variable";
            return false;
        }

        var rawVar = input[(lastSpace + 1)..].Trim();
        var rawTerm = input[..lastSpace].Trim();

        var vn = StripDollar(rawVar);
        if (!IsValidVarName(vn))
        {
            error = $"Invalid variable name '{rawVar}'.";
            return false;
        }

        varName = vn;
        term = Unquote(rawTerm);

        if (string.IsNullOrWhiteSpace(term))
        {
            error = "Search term cannot be empty.";
            return false;
        }

        return true;
    }

    public static string StripDollar(string token) => token.StartsWith('$') ? token[1..] : token;

    public static bool IsValidVarName(string name) =>
        name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') && 
        name.AsSpan().IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_".AsSpan()) < 0;

    public static string Unquote(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
            return s[1..^1];
        return s;
    }

    public static WindowInfo? FindByTitle(IReadOnlyList<WindowInfo> windows, string substr) =>
        FindFirst(windows, w => w.Title.Contains(substr, StringComparison.OrdinalIgnoreCase));

    public static WindowInfo? FindByClass(IReadOnlyList<WindowInfo> windows, string substr) =>
        FindFirst(windows, w => w.Class.Contains(substr, StringComparison.OrdinalIgnoreCase));

    private static WindowInfo? FindFirst(IReadOnlyList<WindowInfo> windows, Func<WindowInfo, bool> predicate)
    {
        foreach (var w in windows)
            if (predicate(w)) return w;
        return null;
    }

    public static void StoreVariable(IDictionary<string, string> variables, string name, string value, int stepNumber)
    {
        if (!IsValidVarName(name))
            throw new InvalidOperationException($"Step {stepNumber}: invalid variable name '{name}'.");
        variables[name] = value;
    }
}
