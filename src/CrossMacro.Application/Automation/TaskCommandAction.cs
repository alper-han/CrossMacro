namespace CrossMacro.Application.Automation;

public enum TaskCommandAction
{
    Add,
    Edit,
    Remove,
    Enable,
    Disable,

    /// <summary>Replaces the hotkeys of a shortcut task.</summary>
    Bind,

    /// <summary>Reports the next run of a schedule task.</summary>
    Next,
}
