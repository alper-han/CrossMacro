
namespace CrossMacro.UI.Services;

public interface ILoadedMacroSession
{
    public ReadOnlyObservableCollection<LoadedMacroListItem> LoadedMacros { get; }

    public LoadedMacroListItem? SelectedMacroItem { get; set; }

    public LoadedMacroPlaybackMode PlaybackMode { get; set; }

    public MacroSequence? SelectedMacro { get; }

    public int Count { get; }

    public event EventHandler? SelectedMacroChanged;

    public event EventHandler? SelectedMacroUpdated;

    public event EventHandler? PlaybackModeChanged;

    public event EventHandler? SessionStateChanged;

    public LoadedMacroListItem AddMacro(MacroSequence macro, string? sourcePath = null);
    public LoadedMacroListItem? UpdateMacro(Guid sessionId, MacroSequence macro, string? sourcePath = null);
    public bool UpdateSelectedMacro(MacroSequence macro);
    public IReadOnlyList<LoadedMacroListItem> CreateSequentialCycleSnapshot();
    public bool RemoveMacro(LoadedMacroListItem item);
    public void RenameSelected(string name);
    public bool SelectNext();
    public LoadedMacroSessionSnapshot CreateSnapshot();
    public void RestoreSnapshot(LoadedMacroSessionSnapshot snapshot);
}
