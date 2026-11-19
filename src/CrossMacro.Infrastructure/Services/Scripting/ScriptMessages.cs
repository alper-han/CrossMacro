namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>Diagnostic texts that several script stages must word identically.</summary>
internal static class ScriptMessages
{
    public static string InvalidVariableName(string name) =>
        $"Invalid variable name '{name}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";

    public static string UnknownVariable(string name) => $"Unknown variable '${name}'.";

    public static string InvalidInteger(string description, string text) =>
        $"Invalid {description} '{text}'. Expected integer.";
}
