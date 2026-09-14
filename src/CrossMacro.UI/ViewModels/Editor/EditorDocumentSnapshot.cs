namespace CrossMacro.UI.ViewModels.Editor;

internal sealed record EditorDocumentSnapshot(EditorStateSnapshot State, string MacroName, IReadOnlyDictionary<string, string> ImageAssets);
