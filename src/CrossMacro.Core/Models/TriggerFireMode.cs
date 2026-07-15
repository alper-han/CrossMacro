namespace CrossMacro.Core.Models;

/// <summary>
/// When the trigger fires relative to the active window state.
/// </summary>
public enum TriggerFireMode
{
    OnceOnChange,
    EveryMatch,
    OnEnter,
    OnExit,
}
