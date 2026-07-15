namespace CrossMacro.Core.Models;

/// <summary>
/// Represents a text expansion entry - a trigger that expands to replacement text
/// </summary>
public class TextExpansion
{
    /// <summary>
    /// The trigger text that will be replaced (e.g., ":mail")
    /// </summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>
    /// The replacement text that will be inserted (e.g., "example@email.com")
    /// </summary>
    public string Replacement { get; set; } = string.Empty;

    /// <summary>
    /// Whether this expansion is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The method used to paste the replacement text
    /// </summary>
    public PasteMethod Method { get; set; } = PasteMethod.CtrlV;

    /// <summary>
    /// The strategy used to insert the replacement text
    /// </summary>
    public TextInsertionMode InsertionMode { get; set; } = TextInsertionMode.Paste;

    /// <summary>
    /// The direct typing method used when <see cref="InsertionMode"/> is <see cref="TextInsertionMode.DirectTyping"/>.
    /// </summary>
    public DirectTypingMethod DirectTypingMethod { get; set; } = DirectTypingMethod.FastBatch;

    /// <summary>
    /// Creates a new text expansion
    /// </summary>
    public TextExpansion()
    {
    }

    /// <summary>
    /// Creates a new text expansion with the specified trigger and replacement
    /// </summary>
    public TextExpansion(
        string trigger,
        string replacement,
        bool isEnabled = true,
        PasteMethod method = PasteMethod.CtrlV,
        TextInsertionMode insertionMode = TextInsertionMode.Paste,
        DirectTypingMethod directTypingMethod = DirectTypingMethod.FastBatch)
    {
        Trigger = trigger;
        Replacement = replacement;
        IsEnabled = isEnabled;
        Method = method;
        InsertionMode = insertionMode;
        DirectTypingMethod = directTypingMethod;
    }
}
