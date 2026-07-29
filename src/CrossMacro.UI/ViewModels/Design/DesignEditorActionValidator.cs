
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignEditorActionValidator : IEditorActionValidator
{
    public (bool IsValid, string? Error) Validate(EditorAction action) => (true, null);

    public (bool IsValid, List<string> Errors) ValidateAll(IEnumerable<EditorAction> actions) => (true, new List<string>());
}
