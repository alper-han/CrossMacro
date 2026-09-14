
namespace CrossMacro.Core.Models.Editing;

public readonly record struct EditorActionWindowPayload(
    WindowCommandMode Mode,
    string SelectorKind,
    string SelectorValue,
    string ActiveField,
    string OutputVariable,
    int TimeoutMs,
    int X,
    int Y,
    int Width,
    int Height,
    string Workspace)
{
    public bool IsValid => Mode switch
    {
        WindowCommandMode.Active => IsValidWindowActiveField(ActiveField)
            && EditorActionScriptTokens.IsValidVariableName(OutputVariable),
        WindowCommandMode.Search => IsValidWindowSearchSelector(SelectorKind)
            && !string.IsNullOrWhiteSpace(SelectorValue)
            && EditorActionScriptTokens.IsValidVariableName(OutputVariable),
        WindowCommandMode.Wait => IsValidWindowSearchSelector(SelectorKind)
            && !string.IsNullOrWhiteSpace(SelectorValue)
            && TimeoutMs > 0
            && EditorActionScriptTokens.IsValidVariableName(OutputVariable),
        WindowCommandMode.Focus => WindowSelectorSyntax.ParseCanonical(SelectorKind) is WindowTargetKind.Active
            || (IsValidWindowFocusSelector(SelectorKind) && !string.IsNullOrWhiteSpace(SelectorValue)),
        WindowCommandMode.Close => WindowSelectorSyntax.ParseCanonical(SelectorKind) is WindowTargetKind.Active
            || (IsValidWindowCloseSelector(SelectorKind) && !string.IsNullOrWhiteSpace(SelectorValue)),
        WindowCommandMode.Resize => Width > 0 && Height > 0,
        WindowCommandMode.WorkspaceGet => EditorActionScriptTokens.IsValidVariableName(OutputVariable),
        WindowCommandMode.WorkspaceSwitch or WindowCommandMode.WorkspaceMoveActive => !string.IsNullOrWhiteSpace(Workspace),
        WindowCommandMode.WorkspaceMoveWindow => !string.IsNullOrWhiteSpace(SelectorValue)
            && !string.IsNullOrWhiteSpace(Workspace),
        WindowCommandMode.Move
            or WindowCommandMode.Center
            or WindowCommandMode.Maximize
            or WindowCommandMode.Fullscreen
            or WindowCommandMode.Floating => true,
        _ => false,
    };

    private static bool IsValidWindowActiveField(string value)
    {
        return WindowActiveFieldSyntax.TryParse(value, out _);
    }

    private static bool IsValidWindowSearchSelector(string value) => WindowSelectorSyntax.IsAllowed(WindowCommandMode.Search, value);

    private static bool IsValidWindowFocusSelector(string value) =>
        WindowSelectorSyntax.IsAllowed(WindowCommandMode.Focus, value) && WindowSelectorSyntax.RequiresValue(WindowSelectorSyntax.ParseCanonical(value));

    private static bool IsValidWindowCloseSelector(string value) =>
        WindowSelectorSyntax.IsAllowed(WindowCommandMode.Close, value) && WindowSelectorSyntax.RequiresValue(WindowSelectorSyntax.ParseCanonical(value));

    public static bool TryCreate(EditorAction action, out EditorActionWindowPayload payload)
    {
        ArgumentNullException.ThrowIfNull(action);
        payload = new EditorActionWindowPayload(
            action.WindowCommandMode,
            action.WindowSelectorKind,
            action.WindowSelectorValue,
            action.WindowActiveField,
            action.WindowOutputVariable,
            action.WindowTimeoutMs,
            action.WindowX,
            action.WindowY,
            action.WindowWidth,
            action.WindowHeight,
            action.WindowWorkspace);
        return action.Type is EditorActionType.WindowCommand;
    }
}
