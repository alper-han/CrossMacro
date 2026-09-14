namespace CrossMacro.UI.ViewModels.Editor;

internal sealed class EditorDocumentSession
{
    private EditorDocumentSnapshot? _saved;
    internal ObservableCollection<EditorAction> Actions { get; } = [];
    internal Dictionary<string, string> ImageAssets { get; } = new(StringComparer.Ordinal);

    internal EditorDocumentSnapshot Capture(string name, bool skipInitialZeroZero) => new(
        new EditorStateSnapshot(Actions.Select(action => action.Clone()).ToList(), skipInitialZeroZero),
        name, new Dictionary<string, string>(ImageAssets, StringComparer.Ordinal));

    internal void MarkClean(EditorDocumentSnapshot snapshot) => _saved = snapshot;

    internal bool IsDirty(string name, bool skipInitialZeroZero)
    {
        if (_saved is null) { return false; }
        if (skipInitialZeroZero != _saved.State.SkipInitialZeroZero || Actions.Count != _saved.State.Actions.Count
            || !string.Equals(name, _saved.MacroName, StringComparison.Ordinal) || ImageAssets.Count != _saved.ImageAssets.Count)
        { return true; }
        for (var index = 0; index < Actions.Count; index++)
        {
            if (!EditorStateComparer.AreActionsEquivalent(Actions[index], _saved.State.Actions[index])) { return true; }
        }
        return ImageAssets.Any(pair => !_saved.ImageAssets.TryGetValue(pair.Key, out var value)
            || !string.Equals(pair.Value, value, StringComparison.Ordinal));
    }
}
