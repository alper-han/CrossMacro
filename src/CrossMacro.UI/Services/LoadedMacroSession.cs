using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Models;

namespace CrossMacro.UI.Services;

public sealed class LoadedMacroSession : ILoadedMacroSession
{
    private readonly ObservableCollection<LoadedMacroListItem> _loadedMacros = new();
    private readonly ILocalizationService? _localizationService;
    private LoadedMacroListItem? _selectedMacroItem;
    private LoadedMacroPlaybackMode _playbackMode;

    public LoadedMacroSession(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        LoadedMacros = new ReadOnlyObservableCollection<LoadedMacroListItem>(_loadedMacros);
    }

    public ReadOnlyObservableCollection<LoadedMacroListItem> LoadedMacros { get; }

    public LoadedMacroListItem? SelectedMacroItem
    {
        get => _selectedMacroItem;
        set
        {
            if (ReferenceEquals(_selectedMacroItem, value))
            {
                return;
            }

            _selectedMacroItem = value;
            SelectedMacroChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public LoadedMacroPlaybackMode PlaybackMode
    {
        get => _playbackMode;
        set
        {
            if (_playbackMode == value)
            {
                return;
            }

            _playbackMode = value;
            PlaybackModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public MacroSequence? SelectedMacro => SelectedMacroItem?.Macro;

    public int Count => _loadedMacros.Count;

    public event EventHandler? SelectedMacroChanged;

    public event EventHandler? SelectedMacroUpdated;

    public event EventHandler? PlaybackModeChanged;

    public LoadedMacroListItem AddMacro(MacroSequence macro, string? sourcePath = null)
    {
        var item = new LoadedMacroListItem(macro, sourcePath, localizationService: _localizationService);
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

        if (SelectedMacroItem == null)
        {
            return false;
        }

        SelectedMacroItem.UpdateMacro(macro);
        RaiseSelectedMacroUpdatedIfNeeded(SelectedMacroItem);
        return true;
    }

    public IReadOnlyList<LoadedMacroListItem> CreateSequentialCycleSnapshot()
    {
        if (_loadedMacros.Count == 0)
        {
            return Array.Empty<LoadedMacroListItem>();
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

        if (!wasSelected)
        {
            return true;
        }

        if (_loadedMacros.Count == 0)
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
        if (SelectedMacroItem == null)
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
        if (_loadedMacros.Count == 0)
        {
            SelectedMacroItem = null;
            return false;
        }

        if (SelectedMacroItem == null)
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
}
