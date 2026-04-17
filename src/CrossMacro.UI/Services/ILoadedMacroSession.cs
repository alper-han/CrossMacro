using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CrossMacro.Core.Models;
using CrossMacro.UI.Models;

namespace CrossMacro.UI.Services;

public interface ILoadedMacroSession
{
    ReadOnlyObservableCollection<LoadedMacroListItem> LoadedMacros { get; }

    LoadedMacroListItem? SelectedMacroItem { get; set; }

    LoadedMacroPlaybackMode PlaybackMode { get; set; }

    MacroSequence? SelectedMacro { get; }

    int Count { get; }

    event EventHandler? SelectedMacroChanged;

    event EventHandler? SelectedMacroUpdated;

    event EventHandler? PlaybackModeChanged;

    LoadedMacroListItem AddMacro(MacroSequence macro, string? sourcePath = null);
    LoadedMacroListItem? UpdateMacro(Guid sessionId, MacroSequence macro, string? sourcePath = null);
    bool UpdateSelectedMacro(MacroSequence macro);
    IReadOnlyList<LoadedMacroListItem> CreateSequentialCycleSnapshot();
    bool RemoveMacro(LoadedMacroListItem item);
    void RenameSelected(string name);
    bool SelectNext();
}
