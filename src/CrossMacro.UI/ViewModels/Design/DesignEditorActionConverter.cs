
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignEditorActionConverter : IEditorActionConverter
{
    public IReadOnlyList<MacroEvent> ToMacroEvents(EditorAction action) => [];

    public EditorAction FromMacroEvent(MacroEvent ev, MacroEvent? nextEvent = null) => new() { Type = EditorActionType.Delay, DelayMs = ev.DelayMs };

    public MacroSequence ToMacroSequence(IEnumerable<EditorAction> actions, string name, bool isAbsolute, bool skipInitialZeroZero = false)
    {
        var macro = DesignPreviewSamples.CreateMacro(name);
        macro.IsAbsoluteCoordinates = isAbsolute;
        macro.SkipInitialZeroZero = skipInitialZeroZero;
        return macro;
    }

    public IReadOnlyList<EditorAction> FromMacroSequence(MacroSequence sequence) => DesignPreviewSamples.CreateEditorActions().ToList();

    public EditorActionRestoreResult FromMacroSequenceWithDiagnostics(MacroSequence sequence)
    {
        return new EditorActionRestoreResult(DesignPreviewSamples.CreateEditorActions().ToList(), new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: true);
    }
}
