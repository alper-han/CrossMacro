using System.Collections.Generic;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services.TextExpansion;

/// <summary>
/// Manages the text buffer and checks for expansion triggers.
/// </summary>
public interface ITextBufferState
{
    /// <summary>
    /// Appends a character to the buffer.
    /// </summary>
    void Append(char c);

    /// <summary>
    /// Handles backspace (removing last character).
    /// </summary>
    void Backspace();

    /// <summary>
    /// Clears the buffer.
    /// </summary>
    void Clear();

    /// <summary>
    /// Checks if the current buffer ends with any of the active triggers.
    /// </summary>
    bool TryGetMatch(IEnumerable<Models.TextExpansion> expansions, out Models.TextExpansion? match);
}
