
namespace CrossMacro.Core.Services;

/// <summary>
/// Converts between EditorAction and MacroEvent/MacroSequence.
/// Follows SRP by focusing solely on conversion logic.
/// </summary>
public interface IEditorActionConverter
{
    /// <summary>
    /// Converts an editor projection into the canonical runtime sequence.
    /// </summary>
    public MacroSequence ToMacroSequence(EditorMacroProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return ToMacroSequence(
            projection.Actions,
            projection.Name,
            projection.IsAbsoluteCoordinates,
            projection.SkipInitialZeroZero);
    }

    /// <summary>
    /// Converts a list of EditorActions to a complete MacroSequence.
    /// </summary>
    /// <param name="actions">The actions to convert.</param>
    /// <param name="name">Name for the macro.</param>
    /// <param name="isAbsolute">Whether coordinates are absolute.</param>
    /// <returns>A playable MacroSequence.</returns>
    public MacroSequence ToMacroSequence(IEnumerable<EditorAction> actions, string name, bool isAbsolute, bool skipInitialZeroZero = false);

    /// <summary>
    /// Converts a single EditorAction to one or more MacroEvents.
    /// Some actions (like KeyPress) expand to multiple events.
    /// </summary>
    /// <param name="action">The editor action to convert.</param>
    /// <returns>List of corresponding MacroEvents.</returns>
    public IReadOnlyList<MacroEvent> ToMacroEvents(EditorAction action);

    /// <summary>
    /// Converts a MacroEvent to an EditorAction.
    /// </summary>
    /// <param name="ev">The macro event to convert.</param>
    /// <returns>The corresponding EditorAction.</returns>
    public EditorAction FromMacroEvent(MacroEvent ev);

    /// <summary>
    /// Restores a runtime sequence into an editor projection.
    /// </summary>
    public EditorMacroProjection FromMacroSequenceProjection(MacroSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        return new EditorMacroProjection(
            FromMacroSequence(sequence),
            sequence.Name,
            sequence.IsAbsoluteCoordinates,
            sequence.SkipInitialZeroZero);
    }

    /// <summary>
    /// Converts a MacroSequence to a list of EditorActions for editing.
    /// </summary>
    /// <param name="sequence">The macro sequence to convert.</param>
    /// <returns>List of EditorActions.</returns>
    public IReadOnlyList<EditorAction> FromMacroSequence(MacroSequence sequence);

    /// <summary>
    /// Converts a MacroSequence to editor actions and returns restore diagnostics.
    /// </summary>
    /// <param name="sequence">The macro sequence to convert.</param>
    /// <returns>Restore result with actions and warnings.</returns>
    public EditorActionRestoreResult FromMacroSequenceWithDiagnostics(MacroSequence sequence);
}
