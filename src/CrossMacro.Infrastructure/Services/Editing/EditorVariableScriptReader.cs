using System.Diagnostics.CodeAnalysis;

namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>Projects variable, arithmetic, clipboard and mouse-position script steps into editable actions.</summary>
internal static class EditorVariableScriptReader
{
    private static readonly (string Keyword, EditorActionType Type)[] ArithmeticCommands =
    [
        ("inc", EditorActionType.IncrementVariable),
        ("dec", EditorActionType.DecrementVariable),
        ("mul", EditorActionType.MultiplyVariable),
        ("div", EditorActionType.DivideVariable),
    ];

    internal static bool TryReadStep(string step, string stepForType, [NotNullWhen(true)] out EditorAction? action)
    {
        if (TryParseMousePositionStep(step, out action) || TryParseSetStep(step, out action) || TryParseClipboardStep(stepForType, out action))
        {
            return true;
        }

        foreach (var (keyword, type) in ArithmeticCommands)
        {
            if (TryParseArithmeticStep(step, keyword, type, out action))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryParseMousePositionStep(string step, [NotNullWhen(true)] out EditorAction? action)
    {
        action = null;
        if (!RunScriptSyntax.IsMousePositionStep(step))
        {
            return false;
        }

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 4
            || !parts[1].Equals(RunScriptSyntax.MousePositionCommand, StringComparison.OrdinalIgnoreCase)
            || !EditorScriptTokenReader.TryNormalizeVariableName(parts[2], out var xVariable)
            || !EditorScriptTokenReader.TryNormalizeVariableName(parts[3], out var yVariable)
            || string.Equals(xVariable, yVariable, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        action = new EditorAction
        {
            Type = EditorActionType.MousePosition,
            MousePositionXVariableName = xVariable,
            MousePositionYVariableName = yVariable,
        };
        return true;
    }

    internal static bool TryParseClipboardStep(string step, [NotNullWhen(true)] out EditorAction? action)
    {
        action = null;
        if (!RunScriptSyntax.StartsWithCommandToken(step.TrimStart(), RunScriptSyntax.ClipboardCommand))
        {
            return false;
        }

        var parts = step.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 3 || !parts[0].Equals(RunScriptSyntax.ClipboardCommand, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts[1].Equals("get", StringComparison.OrdinalIgnoreCase))
        {
            if (EditorScriptTokenReader.TryNormalizeVariableName(parts[2], out var variableName))
            {
                action = new EditorAction { Type = EditorActionType.ClipboardGet, ScriptVariableName = variableName };
            }
        }
        else if (parts[1].Equals("set", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(parts[2]))
            {
                action = new EditorAction { Type = EditorActionType.ClipboardSet, Text = parts[2] };
            }
        }
        else if (parts[1].Equals("capture", StringComparison.OrdinalIgnoreCase))
        {
            action = TryParseClipboardCapture(parts[2]);
        }

        return action is not null;
    }

    internal static bool TryParseSetStep(string step, [NotNullWhen(true)] out EditorAction? action)
    {
        action = null;
        if (!step.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[4..].Trim();
        if (payload.Length is 0)
        {
            return false;
        }

        action = TryParseStructuredSetPayload(payload, out var variableName, out var valueType, out var value)
            ? new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = variableName,
                ScriptValueType = valueType,
                ScriptValue = value,
            }
            : new EditorAction { Type = EditorActionType.SetVariable, Text = payload };
        return true;
    }

    internal static bool TryParseStructuredSetPayload(string payload, out string variableName, out ScriptValueType valueType, out string value)
    {
        variableName = string.Empty;
        valueType = ScriptValueType.Text;
        value = string.Empty;
        var equalIndex = payload.IndexOf('=', StringComparison.Ordinal);
        string rawName;
        string rawValue;
        if (equalIndex > 0)
        {
            rawName = payload[..equalIndex].Trim();
            rawValue = payload[(equalIndex + 1)..].Trim();
        }
        else
        {
            var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            rawName = parts[0];
            rawValue = parts[1].Trim();
        }

        return EditorScriptTokenReader.TryNormalizeVariableName(rawName, out variableName)
            && !string.IsNullOrWhiteSpace(rawValue)
            && EditorScriptTokenReader.TryInferSetValue(rawValue, out valueType, out value);
    }

    /// <summary>
    /// Restores <c>inc/dec/mul/div &lt;name&gt; [amount]</c>. A step whose name or amount is not structured is
    /// kept as the action's legacy text so editing can repair it.
    /// </summary>
    internal static bool TryParseArithmeticStep(string step, string keyword, EditorActionType actionType, [NotNullWhen(true)] out EditorAction? action)
    {
        action = null;
        if (!step.StartsWith($"{keyword} ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[(keyword.Length + 1)..].Trim();
        if (payload.Length is 0)
        {
            return false;
        }

        var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is 0)
        {
            return false;
        }

        var amountToken = parts.Length > 1 ? parts[1] : "1";
        action = EditorScriptTokenReader.TryNormalizeVariableName(parts[0], out var variableName)
            && EditorActionScriptTokens.TryParseNumericToken(amountToken, out var sourceType, out var tokenValue)
                ? new EditorAction
                {
                    Type = actionType,
                    ScriptVariableName = variableName,
                    ScriptNumericSourceType = sourceType,
                    ScriptNumericValue = tokenValue,
                }
                : new EditorAction { Type = actionType, Text = payload };
        return true;
    }

    private static EditorAction? TryParseClipboardCapture(string payload)
    {
        var captureParts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (captureParts.Length is not 2
            || !ClipboardCopyShortcutSyntax.TryParse(captureParts[0], out var shortcut)
            || !EditorScriptTokenReader.TryNormalizeVariableName(captureParts[1], out var variableName))
        {
            return null;
        }

        return new EditorAction
        {
            Type = EditorActionType.CopySelectionToVariable,
            ClipboardCopyShortcut = shortcut,
            ScriptVariableName = variableName,
        };
    }
}
