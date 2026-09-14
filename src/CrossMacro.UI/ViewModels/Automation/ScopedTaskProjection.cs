namespace CrossMacro.UI.ViewModels.Automation;

/// <summary>Owns detached editors and rejects refreshes from an older request or profile.</summary>
internal sealed class ScopedTaskProjection<TTask, TEditor>(
    Func<CancellationToken, Task<TaskCollectionResult<TTask>>> load,
    IUiDispatcher dispatcher,
    Func<TTask, Guid> getId,
    Func<long, TEditor> createEditor,
    Action<TEditor, TTask> loadEditor,
    Func<TEditor?> getSelection,
    Action<TEditor?> setSelection,
    Action changed) : IDisposable
    where TEditor : class
{
    private readonly Dictionary<Guid, TEditor> _editors = [];
    private long _refreshVersion;
    private long _scopeGeneration;
    private int _disposed;

    internal ObservableCollection<TEditor> Items { get; } = [];
    internal long ScopeGeneration => Volatile.Read(ref _scopeGeneration);

    internal bool TryGetEditor(Guid id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TEditor? editor) =>
        _editors.TryGetValue(id, out editor);

    internal async Task RefreshAsync()
    {
        if (Volatile.Read(ref _disposed) is not 0) { return; }
        var version = Interlocked.Increment(ref _refreshVersion);
        var snapshot = await load(CancellationToken.None).ConfigureAwait(false);
        await dispatcher.InvokeAsync(() => Apply(snapshot, version)).ConfigureAwait(false);
    }

    private void Apply(TaskCollectionResult<TTask> snapshot, long version)
    {
        if (Volatile.Read(ref _disposed) is not 0 || version != Volatile.Read(ref _refreshVersion)) { return; }
        var scopeChanged = ScopeGeneration != snapshot.ScopeGeneration;
        if (scopeChanged)
        {
            setSelection(null);
            _editors.Clear();
            Items.Clear();
        }
        Volatile.Write(ref _scopeGeneration, snapshot.ScopeGeneration);

        var currentIds = snapshot.Tasks.Select(getId).ToHashSet();
        foreach (var removed in _editors.Where(pair => !currentIds.Contains(pair.Key)).ToArray())
        {
            _ = Items.Remove(removed.Value);
            _ = _editors.Remove(removed.Key);
        }
        foreach (var task in snapshot.Tasks)
        {
            var id = getId(task);
            if (!_editors.TryGetValue(id, out var editor))
            {
                editor = createEditor(snapshot.ScopeGeneration);
                _editors.Add(id, editor);
                Items.Add(editor);
            }
            loadEditor(editor, task);
        }
        for (var index = 0; index < snapshot.Tasks.Count; index++)
        {
            var existingIndex = Items.IndexOf(_editors[getId(snapshot.Tasks[index])]);
            if (existingIndex != index) { Items.Move(existingIndex, index); }
        }

        var selected = getSelection();
        if (scopeChanged || (selected is not null && !Items.Contains(selected)))
        {
            setSelection(Items.FirstOrDefault());
        }
        changed();
    }

    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
}
