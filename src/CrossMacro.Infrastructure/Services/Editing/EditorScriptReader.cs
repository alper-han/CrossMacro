using System.Diagnostics.CodeAnalysis;

namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>
/// Restores script steps into editor actions. Each step family has its own reader; steps none of them
/// understand are kept verbatim as raw script steps so nothing is lost.
/// </summary>
internal sealed class EditorScriptReader(IKeyCodeMapper keyCodeMapper, EditorEventProjection events)
{
    private readonly EditorInputScriptReader _input = new(keyCodeMapper, events);

    internal bool TryRestoreActionsFromScriptSteps(IList<string>? scriptSteps, out List<EditorAction> actions, out List<EditorActionRestoreWarning> warnings)
    {
        actions = [];
        warnings = [];
        if (scriptSteps is null || scriptSteps.Count is 0)
        {
            return false;
        }

        var cursor = new EditorMouseCursor();
        for (var index = 0; index < scriptSteps.Count; index++)
        {
            var rawStep = scriptSteps[index];
            if (string.IsNullOrWhiteSpace(rawStep))
            {
                continue;
            }

            var step = rawStep.Trim();
            var action = ReadStep(step, rawStep.TrimStart(), cursor, out var warning);
            if (warning is not null)
            {
                warnings.Add(new EditorActionRestoreWarning(index + 1, step, warning));
            }

            actions.Add(action);
        }

        return actions.Count > 0;
    }

    private EditorAction ReadStep(string step, string stepForType, EditorMouseCursor cursor, out string? warning)
    {
        warning = null;
        if (EditorInputScriptReader.TryReadMouseStep(step, cursor, out var action)
            || _input.TryReadInputStep(step, stepForType, out action)
            || EditorVariableScriptReader.TryReadStep(step, stepForType, out action)
            || EditorScreenReadingScriptReader.TryParseScreenReadingStep(step, out action)
            || EditorCommandScriptReader.TryParseShellStep(stepForType, out action)
            || EditorBlockScriptReader.TryReadStep(step, out action))
        {
            return action;
        }

        if (TryReadDedicatedCommand(step, stepForType, out action, out warning))
        {
            return action;
        }

        // A key combination is valid script the structured key actions cannot express; keep it raw without a warning.
        if (!_input.IsKeyCombinationTapStep(step))
        {
            warning = "Unsupported step restored as raw script text.";
        }

        return CreateRawStep(step);
    }

    /// <summary>
    /// Screenshot and window steps are recognised by their command word; a malformed one is kept raw with
    /// a warning that names the command instead of the generic "unsupported" message.
    /// </summary>
    private static bool TryReadDedicatedCommand(string step, string stepForType, [NotNullWhen(true)] out EditorAction? action, out string? warning)
    {
        warning = null;
        action = null;
        if (RunScriptSyntax.IsScreenshotStep(stepForType))
        {
            if (!EditorCommandScriptReader.TryParseScreenshotStep(stepForType, out action))
            {
                warning = "Malformed screenshot step restored as raw script text.";
                action = CreateRawStep(step);
            }

            return true;
        }

        if (RunScriptSyntax.IsWindowStep(stepForType))
        {
            if (!EditorWindowScriptReader.TryParseWindowStep(stepForType, out action))
            {
                warning = "Malformed window step restored as raw script text.";
                action = CreateRawStep(step);
            }

            return true;
        }

        return false;
    }

    private static EditorAction CreateRawStep(string step) => new() { Type = EditorActionType.RawScriptStep, Text = step };
}
