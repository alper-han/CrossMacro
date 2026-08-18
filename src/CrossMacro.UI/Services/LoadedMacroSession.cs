
namespace CrossMacro.UI.Services;

public sealed class LoadedMacroSession : ILoadedMacroSession
{
    private readonly ObservableCollection<LoadedMacroListItem> _loadedMacros = new();
    private readonly ILocalizationService? _localizationService;

    public LoadedMacroSession(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        LoadedMacros = new ReadOnlyObservableCollection<LoadedMacroListItem>(_loadedMacros);
    }

    public ReadOnlyObservableCollection<LoadedMacroListItem> LoadedMacros { get; }

    public LoadedMacroListItem? SelectedMacroItem
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            SelectedMacroChanged?.Invoke(this, EventArgs.Empty);
            SessionStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public LoadedMacroPlaybackMode PlaybackMode
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PlaybackModeChanged?.Invoke(this, EventArgs.Empty);
            SessionStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public MacroSequence? SelectedMacro => SelectedMacroItem?.Macro;

    public int Count => _loadedMacros.Count;

    public event EventHandler? SelectedMacroChanged;

    public event EventHandler? SelectedMacroUpdated;

    public event EventHandler? PlaybackModeChanged;

    public event EventHandler? SessionStateChanged;

    public LoadedMacroListItem AddMacro(MacroSequence macro, string? sourcePath = null)
    {
        var item = CreateItem(macro, sourcePath, sessionId: null);
        _loadedMacros.Add(item);
        SelectedMacroItem = item;
        return item;
    }

    public LoadedMacroListItem? UpdateMacro(Guid sessionId, MacroSequence macro, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(macro);

        foreach (var item in _loadedMacros)
        {
            if (item.SessionId != sessionId)
            {
                continue;
            }

            item.UpdateMacro(macro, sourcePath);
            RaiseSelectedMacroUpdatedIfNeeded(item);
            return item;
        }

        return null;
    }

    public bool UpdateSelectedMacro(MacroSequence macro)
    {
        ArgumentNullException.ThrowIfNull(macro);

        if (SelectedMacroItem is null)
        {
            return false;
        }

        SelectedMacroItem.UpdateMacro(macro);
        RaiseSelectedMacroUpdatedIfNeeded(SelectedMacroItem);
        return true;
    }

    public IReadOnlyList<LoadedMacroListItem> CreateSequentialCycleSnapshot()
    {
        if (_loadedMacros.Count is 0)
        {
            return [];
        }

        var selectedItem = SelectedMacroItem ?? _loadedMacros[0];
        var startIndex = _loadedMacros.IndexOf(selectedItem);
        if (startIndex < 0)
        {
            startIndex = 0;
        }

        var snapshot = new List<LoadedMacroListItem>(_loadedMacros.Count);
        for (var offset = 0; offset < _loadedMacros.Count; offset++)
        {
            snapshot.Add(_loadedMacros[(startIndex + offset) % _loadedMacros.Count].CreateSnapshot());
        }

        return snapshot;
    }

    public bool RemoveMacro(LoadedMacroListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var index = _loadedMacros.IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        var wasSelected = ReferenceEquals(SelectedMacroItem, item);
        _loadedMacros.RemoveAt(index);
        DetachItem(item);

        if (!wasSelected)
        {
            SessionStateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (_loadedMacros.Count is 0)
        {
            SelectedMacroItem = null;
            return true;
        }

        var nextIndex = Math.Min(index, _loadedMacros.Count - 1);
        SelectedMacroItem = _loadedMacros[nextIndex];
        return true;
    }

    public void RenameSelected(string name)
    {
        if (SelectedMacroItem is null)
        {
            return;
        }

        SelectedMacroItem.Name = name;
    }

    private void RaiseSelectedMacroUpdatedIfNeeded(LoadedMacroListItem item)
    {
        if (ReferenceEquals(item, SelectedMacroItem))
        {
            SelectedMacroUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool SelectNext()
    {
        if (_loadedMacros.Count is 0)
        {
            SelectedMacroItem = null;
            return false;
        }

        if (SelectedMacroItem is null)
        {
            SelectedMacroItem = _loadedMacros[0];
            return true;
        }

        var currentIndex = _loadedMacros.IndexOf(SelectedMacroItem);
        if (currentIndex < 0)
        {
            SelectedMacroItem = _loadedMacros[0];
            return true;
        }

        SelectedMacroItem = _loadedMacros[(currentIndex + 1) % _loadedMacros.Count];
        return true;
    }

    public LoadedMacroSessionSnapshot CreateSnapshot()
    {
        var items = _loadedMacros.Select(static item => new LoadedMacroSessionItemSnapshot(
            item.SessionId,
            item.Macro.Clone(),
            item.SourcePath,
            item.SequenceRepeatCount)).ToList();
        return new LoadedMacroSessionSnapshot(items, SelectedMacroItem?.SessionId, (int)PlaybackMode);
    }

    public void RestoreSnapshot(LoadedMacroSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        foreach (var item in _loadedMacros)
        {
            DetachItem(item);
        }

        _loadedMacros.Clear();
        var sessionIds = new HashSet<Guid>();
        foreach (var item in snapshot.Items)
        {
            if (item.SessionId == Guid.Empty || !sessionIds.Add(item.SessionId))
            {
                throw new InvalidDataException("The loaded macro session contains an empty or duplicate session id.");
            }

            var restoredItem = CreateItem(item.Macro.Clone(), item.SourcePath, item.SessionId);
            restoredItem.SequenceRepeatCount = item.SequenceRepeatCount;
            _loadedMacros.Add(restoredItem);
        }

        var selectedItem = snapshot.SelectedSessionId is { } selectedSessionId
            ? _loadedMacros.FirstOrDefault(item => item.SessionId == selectedSessionId)
            : null;
        SelectedMacroItem = selectedItem;
        PlaybackMode = Enum.IsDefined((LoadedMacroPlaybackMode)snapshot.PlaybackMode)
            ? (LoadedMacroPlaybackMode)snapshot.PlaybackMode
            : LoadedMacroPlaybackMode.SelectedOnly;
        SelectedMacroChanged?.Invoke(this, EventArgs.Empty);
        SessionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private LoadedMacroListItem CreateItem(MacroSequence macro, string? sourcePath, Guid? sessionId)
    {
        var item = new LoadedMacroListItem(macro, sourcePath, sessionId, _localizationService);
        item.StateChanged += OnItemStateChanged;
        return item;
    }

    private void DetachItem(LoadedMacroListItem item) => item.StateChanged -= OnItemStateChanged;

    private void OnItemStateChanged(object? sender, EventArgs e) => SessionStateChanged?.Invoke(this, EventArgs.Empty);
}
