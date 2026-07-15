using System.Collections.Generic;

namespace CrossMacro.Core.Models;

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
