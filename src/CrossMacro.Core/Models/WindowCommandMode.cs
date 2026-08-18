namespace CrossMacro.Core.Models;

public enum WindowCommandMode
{
    Active,
    Search,
    Wait,
    Focus,
    Close,
    Move,
    Resize,
    Center,
    Maximize,
    Fullscreen,
    Floating = 10,
    WorkspaceGet,
    WorkspaceSwitch,
    WorkspaceMoveActive,
    WorkspaceMoveWindow,
}
