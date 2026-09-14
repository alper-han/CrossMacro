namespace CrossMacro.UI.ViewModels.Editor;

internal sealed record EditorStateSnapshot(List<EditorAction> Actions, bool SkipInitialZeroZero)
{
    internal EditorStateSnapshot Copy() => new(Actions.Select(action => action.Clone()).ToList(), SkipInitialZeroZero);
}
