namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Captures the live logical cursor position into two run-script variables.
/// </summary>
internal sealed class RunScriptMousePositionExecutor(IMousePositionProvider? mousePositionProvider)
{
    private readonly IMousePositionProvider? _mousePositionProvider = mousePositionProvider;

    public async Task ExecuteStepAsync(
        string step,
        int stepNumber,
        IDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        var error = Validate(step);
        if (error is not null)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {error}");
        }

        if (_mousePositionProvider is null)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: Mouse position requires an IMousePositionProvider runtime service.");
        }

        if (!_mousePositionProvider.IsSupported || !_mousePositionProvider.HasUsableAbsolutePosition())
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: The current mouse position is unavailable in this session.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var position = await _mousePositionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
        if (position is not { } currentPosition)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: The current mouse position is unavailable in this session.");
        }

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var xVariable = RunScriptRuntimeText.NormalizeAndValidateVariableName(parts[2]);
        var yVariable = RunScriptRuntimeText.NormalizeAndValidateVariableName(parts[3]);
        variables[xVariable] = currentPosition.X.ToString(CultureInfo.InvariantCulture);
        variables[yVariable] = currentPosition.Y.ToString(CultureInfo.InvariantCulture);
    }

    public static string? Validate(string step)
    {
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 4
            || !parts[0].Equals(RunScriptSyntax.MouseCommand, StringComparison.OrdinalIgnoreCase)
            || !parts[1].Equals(RunScriptSyntax.MousePositionCommand, StringComparison.OrdinalIgnoreCase))
        {
            return $"Syntax: {RunScriptSyntax.MouseCommand} {RunScriptSyntax.MousePositionCommand} <x_variable> <y_variable>";
        }

        var xVariable = EditorActionScriptTokens.NormalizeVariableToken(parts[2]);
        var yVariable = EditorActionScriptTokens.NormalizeVariableToken(parts[3]);
        if (!EditorActionScriptTokens.IsValidVariableName(xVariable))
        {
            return $"Invalid variable name '{parts[2]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
        }

        if (!EditorActionScriptTokens.IsValidVariableName(yVariable))
        {
            return $"Invalid variable name '{parts[3]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
        }

        if (string.Equals(xVariable, yVariable, StringComparison.OrdinalIgnoreCase))
        {
            return "X and Y destination variables must be different.";
        }

        return null;
    }
}
