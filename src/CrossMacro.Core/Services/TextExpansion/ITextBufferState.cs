
namespace CrossMacro.Core.Services.TextExpansion;

/// <summary>
/// Manages the text buffer and checks for expansion triggers.
/// </summary>
public interface ITextBufferState
{
    /// <summary>
    /// Appends a character to the buffer.
    /// </summary>
    public void Append(char c);

    /// <summary>
    /// Handles backspace (removing last character).
    /// </summary>
    public void Backspace();

    /// <summary>
    /// Clears the buffer.
    /// </summary>
    public void Clear();

    /// <summary>
    /// Checks if the current buffer ends with any of the active triggers.
    /// </summary>
    public bool TryGetMatch(IEnumerable<Models.TextExpansionEntry> expansions, out Models.TextExpansionEntry? match);
}
