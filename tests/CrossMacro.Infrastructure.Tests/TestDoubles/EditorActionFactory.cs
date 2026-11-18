namespace CrossMacro.Infrastructure.Tests.TestDoubles;

internal static class EditorActionFactory
{
    internal static EditorAction CreateValidWindowAction(WindowCommandMode mode)
    {
        return new EditorAction
        {
            Type = EditorActionType.WindowCommand,
            WindowCommandMode = mode,
            WindowSelectorKind = mode is WindowCommandMode.Focus or WindowCommandMode.Close ? "active" : "title",
            WindowSelectorValue = mode is WindowCommandMode.WorkspaceMoveWindow ? "0x123" : "Firefox",
            WindowActiveField = "title",
            WindowOutputVariable = mode is WindowCommandMode.WorkspaceGet ? "workspaceName" : "windowAddress",
            WindowTimeoutMs = 2500,
            WindowX = 100,
            WindowY = 200,
            WindowWidth = 800,
            WindowHeight = 600,
            WindowWorkspace = "2",
        };
    }
}
