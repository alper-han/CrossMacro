namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Derives variable suggestions without mutating a document or depending on view-model lifetime.</summary>
internal static partial class EditorVariableCatalog
{
    internal static string[] GetNames(IReadOnlyList<EditorAction> actions)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < actions.Count; index++)
        {
            var action = actions[index];
            switch (action.Type)
            {
                case EditorActionType.SetVariable:
                    AddIfValidVariableName(names, action.ScriptVariableName);
                    if (action.PreferLegacyScriptText)
                    {
                        TryAddLegacySetVariableName(names, action.Text);
                    }
                    break;
                case EditorActionType.ClipboardGet:
                case EditorActionType.CopySelectionToVariable:
                    AddIfValidVariableName(names, action.ScriptVariableName);
                    break;
                case EditorActionType.ForBlockStart:
                    AddIfValidVariableName(names, action.ForVariableName);
                    break;
                case EditorActionType.MousePosition:
                    AddIfValidVariableName(names, action.MousePositionXVariableName);
                    AddIfValidVariableName(names, action.MousePositionYVariableName);
                    break;
                case EditorActionType.ShellCommand when action.ShellCommandMode is ShellCommandMode.ShellCapture or ShellCommandMode.ShellCaptureInput:
                    AddIfValidVariableName(names, action.ShellExitCodeVariableName);
                    AddIfValidVariableName(names, action.ShellStandardOutputVariableName);
                    AddIfValidVariableName(names, action.ShellStandardErrorVariableName);
                    break;
                case EditorActionType.WindowCommand when action.WindowCommandMode is WindowCommandMode.Active or WindowCommandMode.Search or WindowCommandMode.Wait or WindowCommandMode.WorkspaceGet:
                    AddIfValidVariableName(names, action.WindowOutputVariable);
                    break;
                case EditorActionType.ImageSearch:
                case EditorActionType.ImageClick:
                case EditorActionType.WaitImage:
                    AddIfValidVariableName(names, action.ScreenFoundVariableName);
                    AddIfValidVariableName(names, action.ScreenFoundXVariableName);
                    AddIfValidVariableName(names, action.ScreenFoundYVariableName);
                    break;
            }

            if (action.TryGetScreenReadingPayload(out var screenReadingPayload))
            {
                foreach (var variableName in screenReadingPayload.OutputVariableNames)
                {
                    AddIfValidVariableName(names, variableName);
                }
            }
        }

        return names.Order(StringComparer.Ordinal).ToArray();
    }

    internal static string[] GetColorNames(IReadOnlyList<EditorAction> actions, EditorAction? selectedAction)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var selectedIndex = selectedAction is null ? -1 : Enumerable.Range(0, actions.Count).FirstOrDefault(index => ReferenceEquals(actions[index], selectedAction), -1);
        var actionCount = selectedIndex >= 0 ? selectedIndex : actions.Count;

        for (var index = 0; index < actionCount; index++)
        {
            var action = actions[index];
            if (!action.TryGetScreenReadingPayload(out var screenReadingPayload))
            {
                continue;
            }

            foreach (var variableName in screenReadingPayload.OutputVariableNames
                .Where(name => screenReadingPayload.GetOutputVariableRole(name) is EditorActionScreenReadingVariableRole.Color))
            {
                AddIfValidVariableName(names, variableName);
            }
        }

        return names.Order(StringComparer.Ordinal).ToArray();
    }

    private static void TryAddLegacySetVariableName(ISet<string> target, string legacyText)
    {
        if (string.IsNullOrWhiteSpace(legacyText))
        {
            return;
        }

        var text = legacyText.Trim();
        var equalIndex = text.IndexOf('=', StringComparison.Ordinal);
        if (equalIndex > 0)
        {
            AddIfValidVariableName(target, text[..equalIndex]);
            return;
        }

        var firstPart = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        AddIfValidVariableName(target, firstPart ?? string.Empty);
    }

    private static void AddIfValidVariableName(ISet<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var token = value.Trim();
        if (token.StartsWith('$'))
        {
            token = token[1..];
        }

        if (VariableNameRegex.IsMatch(token))
        {
            _ = target.Add(token);
        }
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex VariableNameRegex { get; }
}
