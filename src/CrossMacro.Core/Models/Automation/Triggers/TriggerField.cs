namespace CrossMacro.Core.Models.Automation.Triggers;

/// <summary>
/// Which window field the trigger matches against.
/// </summary>
public enum TriggerField
{
    WindowClass,
    WindowTitle,
    Workspace,
    ProcessName,
    None,
}
