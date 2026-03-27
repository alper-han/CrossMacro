using System.Collections.Generic;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Converts between EditorAction and MacroEvent/MacroSequence.
/// Follows SRP by focusing solely on conversion logic.
/// </summary>
public interface IEditorActionConverter
{
    /// <summary>
    /// Converts a single EditorAction to one or more MacroEvents.
    /// Some actions (like KeyPress) expand to multiple events.
    /// </summary>
    /// <param name="action">The editor action to convert.</param>
    /// <returns>List of corresponding MacroEvents.</returns>
    List<MacroEvent> ToMacroEvents(EditorAction action);
    
    /// <summary>
    /// Converts a MacroEvent to an EditorAction.
    /// May merge consecutive events (e.g., KeyDown+KeyUp → KeyPress).
    /// </summary>
    /// <param name="ev">The macro event to convert.</param>
    /// <param name="nextEvent">Optional next event for merging detection.</param>
    /// <returns>The corresponding EditorAction.</returns>
    EditorAction FromMacroEvent(MacroEvent ev, MacroEvent? nextEvent = null);
    
    /// <summary>
    /// Converts a list of EditorActions to a complete MacroSequence.
    /// </summary>
    /// <param name="actions">The actions to convert.</param>
    /// <param name="name">Name for the macro.</param>
    /// <param name="isAbsolute">Whether coordinates are absolute.</param>
    /// <returns>A playable MacroSequence.</returns>
    MacroSequence ToMacroSequence(IEnumerable<EditorAction> actions, string name, bool isAbsolute, bool skipInitialZeroZero = false);
    
    /// <summary>
    /// Converts a MacroSequence to a list of EditorActions for editing.
    /// </summary>
    /// <param name="sequence">The macro sequence to convert.</param>
    /// <returns>List of EditorActions.</returns>
    List<EditorAction> FromMacroSequence(MacroSequence sequence);

    /// <summary>
    /// Converts a MacroSequence to editor actions and returns restore diagnostics.
    /// </summary>
    /// <param name="sequence">The macro sequence to convert.</param>
    /// <returns>Restore result with actions and warnings.</returns>
    EditorActionRestoreResult FromMacroSequenceWithDiagnostics(MacroSequence sequence);
}
