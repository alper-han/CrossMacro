
namespace CrossMacro.UI.ViewModels;

/// <summary>
/// Cross-ViewModel communication of the main window: keeps the recording, playback, files and editor
/// view models consistent with each other and forwards their status messages to the global status line.
/// </summary>
internal sealed class MainWindowViewModelLinks : IDisposable
{
    private readonly RecordingViewModel _recording;
    private readonly PlaybackViewModel _playback;
    private readonly FilesViewModel _files;
    private readonly EditorWorkspaceViewModel _editor;
    private readonly ILocalizationService _localizationService;
    private readonly Action<string> _setGlobalStatus;
    private readonly PresentationSubscriptions _subscriptions = new();
    private bool _suppressRecordingStatusForwarding;
    private bool _suppressSelectedMacroRecordingSync;

    internal MainWindowViewModelLinks(
        RecordingViewModel recording,
        PlaybackViewModel playback,
        FilesViewModel files,
        TextExpansionViewModel textExpansion,
        ScheduleViewModel schedule,
        ShortcutViewModel shortcuts,
        TriggerViewModel triggers,
        EditorWorkspaceViewModel editor,
        ILocalizationService localizationService,
        Action<string> setGlobalStatus)
    {
        _recording = recording;
        _playback = playback;
        _files = files;
        _editor = editor;
        _localizationService = localizationService;
        _setGlobalStatus = setGlobalStatus;

        LinkRecording();
        LinkFiles();
        LinkEditor();
        LinkStatus(textExpansion, schedule, shortcuts, triggers);
    }

    public void Dispose() => _subscriptions.Dispose();

    private void LinkRecording()
    {
        // When recording completes, add the macro to the session and select it
        void onRecordingRecordingCompleted(object? s, MacroSequence macro)
        {
            try
            {
                _suppressSelectedMacroRecordingSync = true;
                _files.SetMacro(macro);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[MainWindowViewModel] Failed to sync recorded macro to FilesViewModel");
            }
            finally
            {
                _suppressSelectedMacroRecordingSync = false;
            }

            var eventCount = macro?.Events?.Count ?? 0;
            _setGlobalStatus(string.Format(
                _localizationService.CurrentCulture,
                _localizationService["Status_RecordedEvents"],
                eventCount));
        }
        _recording.RecordingCompleted += onRecordingRecordingCompleted;
        _subscriptions.Add(() => _recording.RecordingCompleted -= onRecordingRecordingCompleted);

        // When recording state changes, update Playback's ability to start
        void onRecordingRecordingStateChanged(object? s, bool isRecording)
        {
            _playback.CanPlayMacroExternal = !isRecording;
        }
        _recording.RecordingStateChanged += onRecordingRecordingStateChanged;
        _subscriptions.Add(() => _recording.RecordingStateChanged -= onRecordingRecordingStateChanged);

        // When playback state changes, update Recording's ability to start and freeze Files interactions
        void onPlaybackPlaybackStateChanged(object? s, bool isPlaying)
        {
            _recording.CanStartRecordingExternal = !isPlaying;
            _files.CanManageLoadedMacrosExternal = !isPlaying;

            if (!isPlaying)
            {
                SyncRecordingMacroSummary();
            }
        }
        _playback.PlaybackStateChanged += onPlaybackPlaybackStateChanged;
        _subscriptions.Add(() => _playback.PlaybackStateChanged -= onPlaybackPlaybackStateChanged);

        // Forward status changes
        void onRecordingPropertyChanged(object? s, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(_recording.RecordingStatus), StringComparison.Ordinal) && !_suppressRecordingStatusForwarding)
            {
                _setGlobalStatus(_recording.RecordingStatus);
            }
        }
        _recording.PropertyChanged += onRecordingPropertyChanged;
        _subscriptions.Add(() => _recording.PropertyChanged -= onRecordingPropertyChanged);
    }

    private void LinkFiles()
    {
        void SyncSelectedMacroSummary(object? _, EventArgs __)
        {
            if (_suppressSelectedMacroRecordingSync)
            {
                return;
            }

            SyncRecordingMacroSummary();
        }

        // Keep recording statistics in sync when selection changes or the selected macro payload is replaced.
        _files.SelectedMacroChanged += SyncSelectedMacroSummary;
        _subscriptions.Add(() => _files.SelectedMacroChanged -= SyncSelectedMacroSummary);
        _files.SelectedMacroUpdated += SyncSelectedMacroSummary;
        _subscriptions.Add(() => _files.SelectedMacroUpdated -= SyncSelectedMacroSummary);

        // When a macro is loaded from disk, update global status.
        void onFilesMacroLoaded(object? s, MacroSequence macro)
        {
            _setGlobalStatus(string.Format(
                _localizationService.CurrentCulture,
                _localizationService["Status_LoadedMacro"],
                macro.Name));
        }
        _files.MacroLoaded += onFilesMacroLoaded;
        _subscriptions.Add(() => _files.MacroLoaded -= onFilesMacroLoaded);

        void onLoadedMacroRemoved(object? sender, Guid sessionId) => _editor.ClearLoadedMacroSessionLink(sessionId);
        _files.LoadedMacroRemoved += onLoadedMacroRemoved;
        _subscriptions.Add(() => _files.LoadedMacroRemoved -= onLoadedMacroRemoved);
    }

    private void LinkEditor()
    {
        // A successful editor save refreshes loaded copies, including independently opened files.
        void onEditorMacroCreated(object? s, EditorDocumentMacroCreatedEventArgs e)
        {
            var linkedItem = _files.UpsertMacro(
                e.Document.LinkedLoadedMacroSessionId,
                e.MacroCreated.Macro,
                e.MacroCreated.SourcePath,
                addIfMissing: e.Document.ShouldAddToPlaybackOnSave);

            if (linkedItem is not null)
            {
                e.Document.TrackLoadedMacroSession(linkedItem.SessionId);
            }
            else if (e.Document.LinkedLoadedMacroSessionId is not null)
            {
                e.Document.ClearLoadedMacroSessionLink();
            }

            _setGlobalStatus(string.Format(
                _localizationService.CurrentCulture,
                _localizationService["Status_CreatedMacro"],
                e.MacroCreated.Macro.Name,
                MacroPlayableActionCounter.CountPlayableActions(e.MacroCreated.Macro)));
        }
        _editor.MacroCreated += onEditorMacroCreated;
        _subscriptions.Add(() => _editor.MacroCreated -= onEditorMacroCreated);

        void onEditorPlaybackAddRequested(object? s, EditorDocumentPlaybackAddRequestedEventArgs e)
        {
            var item = _files.UpsertMacro(sessionId: null, e.PlaybackRequested.Macro, e.PlaybackRequested.SourcePath);
            if (item is not null)
            {
                _files.SelectedMacroItem = item;
                e.Document.TrackLoadedMacroSession(item.SessionId);
            }
        }
        _editor.PlaybackAddRequested += onEditorPlaybackAddRequested;
        _subscriptions.Add(() => _editor.PlaybackAddRequested -= onEditorPlaybackAddRequested);
    }

    private void LinkStatus(
        TextExpansionViewModel textExpansion,
        ScheduleViewModel schedule,
        ShortcutViewModel shortcuts,
        TriggerViewModel triggers)
    {
        void onStatusChanged(object? s, string status) => _setGlobalStatus(status);
        _playback.StatusChanged += onStatusChanged;
        _subscriptions.Add(() => _playback.StatusChanged -= onStatusChanged);
        _files.StatusChanged += onStatusChanged;
        _subscriptions.Add(() => _files.StatusChanged -= onStatusChanged);
        textExpansion.StatusChanged += onStatusChanged;
        _subscriptions.Add(() => textExpansion.StatusChanged -= onStatusChanged);
        schedule.StatusChanged += onStatusChanged;
        _subscriptions.Add(() => schedule.StatusChanged -= onStatusChanged);
        shortcuts.StatusChanged += onStatusChanged;
        _subscriptions.Add(() => shortcuts.StatusChanged -= onStatusChanged);
        triggers.StatusChanged += onStatusChanged;
        _subscriptions.Add(() => triggers.StatusChanged -= onStatusChanged);
        _editor.StatusChanged += onStatusChanged;
        _subscriptions.Add(() => _editor.StatusChanged -= onStatusChanged);
    }

    private void SyncRecordingMacroSummary()
    {
        if (_playback.IsPlaying || _recording.IsRecording)
        {
            return;
        }

        try
        {
            _suppressRecordingStatusForwarding = true;
            _recording.SetMacro(_files.CurrentMacro, updateStatus: true);
        }
        finally
        {
            _suppressRecordingStatusForwarding = false;
        }
    }
}
