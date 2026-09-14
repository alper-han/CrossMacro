namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>Projects runtime-validated window command tokens into editable actions.</summary>
internal static class EditorWindowScriptReader
{
    internal static bool TryParseWindowStep(string step, out EditorAction action)
    {
        action = new EditorAction();
        var trimmed = step.Trim();
        var validationError = Playback.RunScriptWindowExecutor.Validate(trimmed);
        if (validationError is not null)
        {
            return false;
        }

        var parts = RunScriptSyntax.SplitQuotedTokens(trimmed).ToArray();
        if (parts.Length < 2 || !parts[0].Equals("window", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        switch (parts[1].ToUpperInvariant())
        {
            case "ACTIVE":
                action = CreateWindowAction(WindowCommandMode.Active, activeField: parts[2], outputVariable: parts[3]);
                return true;
            case "SEARCH":
                return TryParseWindowSearch(parts, out action);
            case "WAIT":
                return TryParseWindowWait(parts, out action);
            case "FOCUS":
                return TryParseWindowSelectorCommand(parts, WindowCommandMode.Focus, out action);
            case "CLOSE":
                return TryParseWindowSelectorCommand(parts, WindowCommandMode.Close, out action);
            case "MOVE":
                action = CreateWindowAction(WindowCommandMode.Move, x: int.Parse(parts[2], CultureInfo.InvariantCulture), y: int.Parse(parts[3], CultureInfo.InvariantCulture));
                return true;
            case "RESIZE":
                action = CreateWindowAction(WindowCommandMode.Resize, width: int.Parse(parts[2], CultureInfo.InvariantCulture), height: int.Parse(parts[3], CultureInfo.InvariantCulture));
                return true;
            case "CENTER":
                action = CreateWindowAction(WindowCommandMode.Center);
                return true;
            case "MAXIMIZE":
                action = CreateWindowAction(WindowCommandMode.Maximize);
                return true;
            case "FULLSCREEN":
                action = CreateWindowAction(WindowCommandMode.Fullscreen);
                return true;
            case "FLOAT":
                action = CreateWindowAction(WindowCommandMode.Floating);
                return true;
            case "GETDESKTOP":
                action = CreateWindowAction(WindowCommandMode.WorkspaceGet, outputVariable: parts[2]);
                return true;
            case "SETDESKTOP":
                action = CreateWindowAction(WindowCommandMode.WorkspaceSwitch, workspace: UnquoteWindowField(string.Join(' ', parts[2..])));
                return true;
            case "SETDESKTOPFORWINDOW":
                return TryParseWindowWorkspaceMove(parts, out action);
            default:
                return false;
        }
    }

    private static bool TryParseWindowSearch(string[] parts, out EditorAction action)
    {
        action = CreateWindowAction(WindowCommandMode.Search,
            selectorKind: parts[2], selectorValue: UnquoteWindowField(string.Join(' ', parts[3..^1])),
            outputVariable: parts[^1]);
        return true;
    }

    private static bool TryParseWindowWait(string[] parts, out EditorAction action)
    {
        var timeoutMs = 5000;
        var termEnd = parts.Length - 1;
        if (parts.Length > 4 && int.TryParse(parts[^2], NumberStyles.None, CultureInfo.InvariantCulture, out var timeout) && timeout > 0)
        {
            timeoutMs = timeout;
            termEnd--;
        }
        action = CreateWindowAction(WindowCommandMode.Wait,
            selectorKind: parts[2], selectorValue: UnquoteWindowField(string.Join(' ', parts[3..termEnd])),
            outputVariable: parts[^1], timeoutMs: timeoutMs);
        return true;
    }

    private static bool TryParseWindowSelectorCommand(string[] parts, WindowCommandMode mode, out EditorAction action)
    {
        var selectorKind = NormalizeSelectorKind(parts[2]);
        action = CreateWindowAction(mode, selectorKind: selectorKind,
            selectorValue: selectorKind is "active" ? string.Empty : UnquoteWindowField(string.Join(' ', parts[3..])));
        return true;
    }

    internal static bool TryParseWindowWorkspaceMove(string[] parts, out EditorAction action)
    {
        action = new EditorAction();
        if (parts.Length < 4)
        {
            return false;
        }

        var selectorKind = parts[2].ToUpperInvariant();
        if (selectorKind is "ACTIVE")
        {
            action = CreateWindowAction(WindowCommandMode.WorkspaceMoveActive, workspace: UnquoteWindowField(string.Join(' ', parts[3..])));
            return true;
        }

        if (selectorKind is "ADDRESS" && parts.Length >= 5)
        {
            action = CreateWindowAction(WindowCommandMode.WorkspaceMoveWindow, selectorKind: "address", selectorValue: parts[3], workspace: UnquoteWindowField(string.Join(' ', parts[4..])));
            return true;
        }

        return false;
    }

    internal static string NormalizeSelectorKind(string value) => value.Trim().ToUpperInvariant() switch
    {
        "TITLE" => "title",
        "CLASS" => "class",
        "ADDRESS" => "address",
        "ACTIVE" => "active",
        _ => value.Trim(),
    };
    internal static string UnquoteWindowField(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length < 2 || !((trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            return trimmed;
        }

        var quote = trimmed[0];
        var builder = new StringBuilder();
        for (var index = 1; index < trimmed.Length - 1; index++)
        {
            if (trimmed[index] == '\\' && index + 1 < trimmed.Length - 1 && (trimmed[index + 1] == quote || trimmed[index + 1] == '\\'))
            {
                _ = builder.Append(trimmed[index + 1]);
                index++;
                continue;
            }

            _ = builder.Append(trimmed[index]);
        }

        return builder.ToString();
    }

    internal static EditorAction CreateWindowAction(WindowCommandMode mode, string selectorKind = "title", string selectorValue = "", string activeField = "title", string outputVariable = "windowResult", int timeoutMs = 5000, int x = 0, int y = 0, int width = 1280, int height = 720, string workspace = "")
    {
        return new EditorAction
        {
            Type = EditorActionType.WindowCommand,
            WindowCommandMode = mode,
            WindowSelectorKind = selectorKind,
            WindowSelectorValue = selectorValue,
            WindowActiveField = activeField,
            WindowOutputVariable = EditorActionScriptTokens.NormalizeVariableToken(outputVariable),
            WindowTimeoutMs = timeoutMs,
            WindowX = x,
            WindowY = y,
            WindowWidth = width,
            WindowHeight = height,
            WindowWorkspace = workspace,
        };
    }

}
