namespace CrossMacro.Core.Services.Hotkeys;

/// <summary>
/// Represents a parsed hotkey mapping
/// </summary>
public class HotkeyMapping
{
    /// <summary>
    /// The main key code
    /// </summary>
    public int KeyCode { get; set; }
    
    /// <summary>
    /// Whether Ctrl modifier is required
    /// </summary>
    public bool RequiresCtrl { get; set; }
    
    /// <summary>
    /// Whether Alt modifier is required
    /// </summary>
    public bool RequiresAlt { get; set; }
    
    /// <summary>
    /// Whether Shift modifier is required
    /// </summary>
    public bool RequiresShift { get; set; }
    
    /// <summary>
    /// Whether Super (Meta/Windows) modifier is required
    /// </summary>
    public bool RequiresSuper { get; set; }
    
    /// <summary>
    /// The original hotkey string
    /// </summary>
    public string HotkeyString { get; set; } = string.Empty;
}

/// <summary>
/// Interface for parsing hotkey strings (e.g., "Ctrl+Shift+F8")
/// Single Responsibility: Parse and build hotkey strings
/// </summary>
public interface IHotkeyParser
{
    /// <summary>
    /// Parse a hotkey string into a HotkeyMapping
    /// </summary>
    HotkeyMapping Parse(string hotkeyString);
    
    /// <summary>
    /// Build a hotkey string from components
    /// </summary>
    string BuildHotkeyString(int keyCode, bool ctrl, bool alt, bool shift, bool super);
    
    /// <summary>
    /// Get human-readable key name from key code
    /// </summary>
    string GetKeyName(int keyCode);
    
    /// <summary>
    /// Get key code from key name
    /// </summary>
    int GetKeyCode(string keyName);
    
    /// <summary>
    /// Check if a key code is a modifier key
    /// </summary>
    bool IsModifierKey(int keyCode);
}
