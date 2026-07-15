namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Represents a parsed hotkey configuration.
/// </summary>
public class HotkeyMapping
{
    /// <summary>
    /// The main key code (non-modifier key).
    /// </summary>
    public int MainKey { get; set; } = -1;

    /// <summary>
    /// Set of required modifier key codes.
    /// </summary>
    public HashSet<int> RequiredModifiers { get; set; } = new();

    /// <summary>
    /// Indicates if this mapping is valid (has a main key).
    /// </summary>
    public bool IsValid => MainKey != -1;
}
