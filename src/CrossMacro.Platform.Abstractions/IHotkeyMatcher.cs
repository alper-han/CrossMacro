namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Matches input key codes against configured hotkey mappings.
/// </summary>
public interface IHotkeyMatcher
{
    /// <summary>
    /// Attempts to match a key press against a hotkey mapping.
    /// Includes debouncing to prevent rapid-fire triggering.
    /// </summary>
    /// <param name="keyCode">The pressed key code</param>
    /// <param name="modifiers">Currently pressed modifier key codes</param>
    /// <param name="mapping">The hotkey mapping to match against</param>
    /// <param name="actionName">Name of the action for debounce tracking</param>
    /// <returns>True if the hotkey matches and should trigger</returns>
    bool TryMatch(int keyCode, IReadOnlySet<int> modifiers, HotkeyMapping mapping, string actionName);

    /// <summary>
    /// Resets the debounce state for all actions.
    /// </summary>
    void ResetDebounce();

    /// <summary>
    /// Gets or sets the debounce interval in milliseconds.
    /// </summary>
    int DebounceIntervalMs { get; set; }
}
