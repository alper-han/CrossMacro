using System.Collections.Generic;

namespace CrossMacro.Core.Models;

/// <summary>
/// Which window field the trigger matches against.
/// </summary>
public enum TriggerField
{
    WindowClass,
    WindowTitle,
    Workspace,
    ProcessName,
    None
}

/// <summary>
/// How the field value is compared to the task's value.
/// </summary>
public enum TriggerMatchMode
{
    Equals,
    Contains,
    Regex
}

/// <summary>
/// What the trigger does when it fires.
/// </summary>
public enum TriggerAction
{
    SwitchProfile,
    RunMacro
}

/// <summary>
/// When the trigger fires relative to the active window state.
/// </summary>
public enum TriggerFireMode
{
    OnceOnChange,
    EveryMatch,
    OnEnter,
    OnExit
}

/// <summary>
/// Static list sources for enum-bound ComboBoxes in the trigger UI.
/// Avoids per-instance allocation; one read-only list per enum type.
/// </summary>
public static class TriggerEnums
{
    public static readonly IReadOnlyList<TriggerField> AvailableFields =
        System.Enum.GetValues<TriggerField>();

    public static readonly IReadOnlyList<TriggerMatchMode> AvailableMatchModes =
        System.Enum.GetValues<TriggerMatchMode>();

    public static readonly IReadOnlyList<TriggerAction> AvailableActions =
        System.Enum.GetValues<TriggerAction>();

    public static readonly IReadOnlyList<TriggerFireMode> AvailableFireModes =
        System.Enum.GetValues<TriggerFireMode>();
}
