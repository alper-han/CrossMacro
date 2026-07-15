
namespace CrossMacro.Core.Services;

/// <summary>
/// Validates EditorAction instances.
/// Follows ISP by providing a focused validation interface.
/// </summary>
public interface IEditorActionValidator
{
    /// <summary>
    /// Validates a single EditorAction.
    /// </summary>
    /// <param name="action">The action to validate.</param>
    /// <returns>Tuple of (IsValid, ErrorMessage).</returns>
    (bool IsValid, string? Error) Validate(EditorAction action);

    /// <summary>
    /// Validates a collection of EditorActions.
    /// </summary>
    /// <param name="actions">The actions to validate.</param>
    /// <returns>Tuple of (IsValid, ErrorMessages).</returns>
    (bool IsValid, List<string> Errors) ValidateAll(IEnumerable<EditorAction> actions);
}
