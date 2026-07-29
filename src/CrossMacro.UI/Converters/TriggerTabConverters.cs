
namespace CrossMacro.UI.Converters;

/// <summary>
/// Converters for the Trigger tab UI.
/// </summary>
public static class TriggerTabConverters
{
    /// <summary>
    /// True when the trigger action is SwitchProfile — used to surface the target profile picker.
    /// </summary>
    public static readonly IValueConverter IsSwitchProfileConverter =
        new FuncValueConverter<TriggerOperation, bool>(action => action is TriggerOperation.SwitchProfile);

    public static readonly IValueConverter IsRunMacroConverter =
        new FuncValueConverter<TriggerOperation, bool>(action => action is TriggerOperation.RunMacro);

    /// <summary>
    /// True when the trigger field requires match-mode + value entry (i.e. not None/Interval).
    /// </summary>
    public static readonly IValueConverter NeedsMatchValueConverter =
        new FuncValueConverter<TriggerField, bool>(field => field is not TriggerField.None);

    /// <summary>
    /// True when an integer count is greater than zero — used to show the window picker ComboBox
    /// only after Refresh has populated it.
    /// </summary>
    public static readonly IValueConverter IsNonZeroConverter =
        new FuncValueConverter<int, bool>(count => count > 0);
}
