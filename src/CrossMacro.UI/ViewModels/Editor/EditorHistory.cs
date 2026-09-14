namespace CrossMacro.UI.ViewModels.Editor;

internal sealed class EditorHistory(TimeProvider timeProvider)
{
    private const int Limit = 2000;
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(400);
    private readonly Stack<EditorStateSnapshot> _undo = new(Limit);
    private readonly Stack<EditorStateSnapshot> _redo = new(Limit);
    private EditorAction? _lastAction;
    private string? _lastProperty;
    private long _lastEditTimestamp;

    internal EditorStateSnapshot LastKnownState { get; private set; } = new([], SkipInitialZeroZero: false);
    internal bool CanUndo => _undo.Count > 0;
    internal bool CanRedo => _redo.Count > 0;
    internal void Remember(EditorStateSnapshot state) => LastKnownState = state;

    internal bool ShouldCoalesce(EditorAction? action, string propertyName)
    {
        propertyName = propertyName switch
        {
            nameof(EditorAction.X) or nameof(EditorAction.CoordinateXToken) => nameof(EditorAction.CoordinateXToken),
            nameof(EditorAction.Y) or nameof(EditorAction.CoordinateYToken) => nameof(EditorAction.CoordinateYToken),
            nameof(EditorAction.DelayMicroseconds) or nameof(EditorAction.DelayMs) or nameof(EditorAction.DelayDuration) => nameof(EditorAction.DelayMicroseconds),
            _ => propertyName,
        };
        var now = timeProvider.GetTimestamp();
        var coalesce = action is not null && ReferenceEquals(action, _lastAction)
            && string.Equals(propertyName, _lastProperty, StringComparison.Ordinal)
            && timeProvider.GetElapsedTime(_lastEditTimestamp, now) <= CoalesceWindow;
        _lastAction = action;
        _lastProperty = propertyName;
        _lastEditTimestamp = now;
        return coalesce;
    }

    internal void ResetCoalescing()
    {
        _lastAction = null;
        _lastProperty = null;
        _lastEditTimestamp = 0;
    }

    internal void Save(EditorStateSnapshot state)
    {
        if (_undo.Count is 0 || !EditorStateComparer.AreStatesEquivalent(_undo.Peek(), state))
        {
            _undo.Push(state.Copy());
            if (_undo.Count > Limit)
            {
                var retained = _undo.Take(Limit).Reverse().ToArray();
                _undo.Clear();
                foreach (var snapshot in retained) { _undo.Push(snapshot); }
            }
        }
        _redo.Clear();
    }

    internal EditorStateSnapshot Undo(EditorStateSnapshot current)
    {
        _redo.Push(current);
        return _undo.Pop();
    }

    internal EditorStateSnapshot Redo(EditorStateSnapshot current)
    {
        _undo.Push(current);
        return _redo.Pop();
    }

    internal void Clear() { _undo.Clear(); _redo.Clear(); ResetCoalescing(); }
}
