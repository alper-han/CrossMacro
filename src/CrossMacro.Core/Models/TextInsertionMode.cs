namespace CrossMacro.Core.Models;

/// <summary>
/// Defines how the replacement text should be inserted
/// </summary>
public enum TextInsertionMode
{
    /// <summary>
    /// Insert by placing the text on the clipboard and pasting it
    /// </summary>
    Paste,

    /// <summary>
    /// Insert by simulating direct typing without using the clipboard
    /// </summary>
    DirectTyping,
}
