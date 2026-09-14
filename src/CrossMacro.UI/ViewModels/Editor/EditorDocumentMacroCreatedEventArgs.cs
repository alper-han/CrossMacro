namespace CrossMacro.UI.ViewModels.Editor;

public sealed class EditorDocumentMacroCreatedEventArgs(EditorViewModel document, EditorMacroCreatedEventArgs macroCreated) : EventArgs
{
    public EditorViewModel Document { get; } = document ?? throw new ArgumentNullException(nameof(document));

    public EditorMacroCreatedEventArgs MacroCreated { get; } = macroCreated ?? throw new ArgumentNullException(nameof(macroCreated));
}
