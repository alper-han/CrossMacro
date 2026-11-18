using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>
/// The variable scope of one script run over <paramref name="store"/> (which must compare names
/// case-insensitively). Static expansion and live playback store, read and substitute variables through this
/// one type, so a <c>$name</c> reference means the same thing everywhere. The read-only face is what the Core
/// numeric and condition evaluators consume.
/// </summary>
internal sealed class RunScriptVariables(IDictionary<string, string> store) : IReadOnlyDictionary<string, string>
{
    private readonly IDictionary<string, string> _store = store ?? throw new ArgumentNullException(nameof(store));

    public RunScriptVariables()
        : this(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
    {
    }

    public readonly record struct ResolutionError(string Message, bool IsUnknownVariable);

    public int Count => _store.Count;

    public IEnumerable<string> Keys => _store.Keys;

    public IEnumerable<string> Values => _store.Values;

    public string this[string key] => _store[key];

    public void Set(string name, string value) => _store[name] = value;

    public bool ContainsKey(string key) => _store.ContainsKey(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) => _store.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _store.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Substitutes every <c>$name</c> in <paramref name="input"/> with the variable's value and collapses
    /// <c>$$</c> to a literal <c>$</c>. Malformed references are described in the wording of <paramref name="dialect"/>.
    /// </summary>
    public bool TryResolveText(string input, RunScriptDialect dialect, out string resolved, out ResolutionError error) =>
        TryResolveText(_store, input, dialect, out resolved, out error);

    /// <inheritdoc cref="TryResolveText(string, RunScriptDialect, out string, out ResolutionError)"/>
    public static bool TryResolveText(
        IDictionary<string, string> variables,
        string input,
        RunScriptDialect dialect,
        out string resolved,
        out ResolutionError error)
    {
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(dialect);

        error = default;
        if (!input.Contains('$', StringComparison.Ordinal))
        {
            resolved = input;
            return true;
        }

        var output = new StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            var current = input[i];
            if (current != '$')
            {
                _ = output.Append(current);
                continue;
            }

            if (i + 1 < input.Length && input[i + 1] == '$')
            {
                _ = output.Append('$');
                i++;
                continue;
            }

            if (i + 1 >= input.Length || !ScriptIdentifierSyntax.IsVariableNameStart(input[i + 1]))
            {
                resolved = string.Empty;
                error = new ResolutionError(dialect.InvalidVariableReference(input, i), IsUnknownVariable: false);
                return false;
            }

            var end = i + 2;
            while (end < input.Length && ScriptIdentifierSyntax.IsVariableNamePart(input[end]))
            {
                end++;
            }

            var name = input[(i + 1)..end];
            if (!variables.TryGetValue(name, out var value))
            {
                resolved = string.Empty;
                error = new ResolutionError(ScriptMessages.UnknownVariable(name), IsUnknownVariable: true);
                return false;
            }

            _ = output.Append(value);
            i = end - 1;
        }

        resolved = output.ToString();
        return true;
    }
}
