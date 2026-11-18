namespace CrossMacro.Commands.Execution;

/// <summary>
/// The single rule for run image asset names, shared by the CLI parser and the MCP tools:
/// a script variable name without a leading <c>$</c>.
/// </summary>
public static class RunImageAssetNames
{
    public const string Pattern = "[A-Za-z_][A-Za-z0-9_]*";

    public static bool IsValid(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && !name.StartsWith('$')
        && ScriptIdentifierSyntax.IsValidVariableName(name);
}
