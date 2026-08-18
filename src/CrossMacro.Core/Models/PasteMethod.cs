namespace CrossMacro.Core.Models;

/// <summary>
/// Defines how the replacement text should be inserted
/// </summary>
public enum PasteMethod
{
    /// <summary>
    /// Standard GUI paste (Ctrl+V on Linux/Windows, Command+V on macOS)
    /// </summary>
    CtrlV,

    /// <summary>
    /// Terminal paste (Ctrl+Shift+V)
    /// </summary>
    CtrlShiftV,

    /// <summary>
    /// Legacy paste (Shift+Insert)
    /// </summary>
    ShiftInsert,
}
