
namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Files tab - handles macro save/load operations
/// </summary>
public partial class FilesViewModel : ViewModelBase
{
    private enum FilesStatusKind
    {
        Ready,
        LoadCancelled,
        SaveCancelled,
        Other,
    }

    private const string DefaultMacroName = MacroNameDefaults.NewRecordedMacroName;
    private const int DefaultSequenceRepeatCount = 1;

    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    private readonly ILoadedMacroSession _loadedMacroSession;
    private readonly ILocalizationService _localizationService;

    private string _macroName = DefaultMacroName;
    private string _status;
    private FilesStatusKind _statusKind = FilesStatusKind.Ready;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMacro))]
    [NotifyPropertyChangedFor(nameof(CanSaveMacro))]
    private bool _canManageLoadedMacrosExternal = true;

    /// <summary>
    /// Event fired when a macro is loaded from disk.
    /// </summary>
    public event EventHandler<MacroSequence>? MacroLoaded;

    /// <summary>
    /// Event fired when the selected macro changes.
    /// </summary>
    public event EventHandler? SelectedMacroChanged;

    /// <summary>
    /// Event fired when the selected macro payload is updated in place.
    /// </summary>
    public event EventHandler? SelectedMacroUpdated;

    /// <summary>
    /// Event fired when status changes.
    /// </summary>
    public event EventHandler<string>? StatusChanged;

    public FilesViewModel(
        IMacroFileManager fileManager,
        IDialogService dialogService,
        ILoadedMacroSession loadedMacroSession,
        ILocalizationService localizationService)
    {
        _fileManager = fileManager;
        _dialogService = dialogService;
        _loadedMacroSession = loadedMacroSession;
        _localizationService = localizationService;
        _status = BuildStatus(FilesStatusKind.Ready);
        _localizationService.CultureChanged += OnCultureChanged;

        _loadedMacroSession.SelectedMacroChanged += OnSelectedMacroChanged;
        _loadedMacroSession.SelectedMacroUpdated += OnSelectedMacroUpdated;
        _loadedMacroSession.PlaybackModeChanged += OnPlaybackModeChanged;
        SyncFromSelectedMacro();
    }

    public ReadOnlyObservableCollection<LoadedMacroListItem> LoadedMacros => _loadedMacroSession.LoadedMacros;

    public LoadedMacroListItem? SelectedMacroItem
    {
        get => _loadedMacroSession.SelectedMacroItem;
        set
        {
            if (ReferenceEquals(_loadedMacroSession.SelectedMacroItem, value))
            {
                return;
            }

            _loadedMacroSession.SelectedMacroItem = value;
        }
    }

    public bool HasLoadedMacros => _loadedMacroSession.Count > 0;

    // Kept manual: normalizes (coerces) the incoming value and branches between session rename and local field.
    public string MacroName
    {
        get => _loadedMacroSession.SelectedMacroItem?.Name ?? _macroName;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? DefaultMacroName : value.Trim();
            var selectedItem = _loadedMacroSession.SelectedMacroItem;
            if (selectedItem is not null)
            {
                if (string.Equals(selectedItem.Name, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _loadedMacroSession.RenameSelected(normalized);
                OnPropertyChanged();
                return;
            }

            if (string.Equals(_macroName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _macroName = normalized;
            OnPropertyChanged();
        }
    }

    public int SelectedSequenceRepeatCount
    {
        get => _loadedMacroSession.SelectedMacroItem?.SequenceRepeatCount ?? DefaultSequenceRepeatCount;
        set
        {
            var selectedItem = _loadedMacroSession.SelectedMacroItem;
            if (selectedItem is null)
            {
                return;
            }

            var normalized = Math.Max(DefaultSequenceRepeatCount, value);
            if (selectedItem.SequenceRepeatCount == normalized)
            {
                return;
            }

            selectedItem.SequenceRepeatCount = normalized;
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveMacro))]
    public partial bool HasRecordedMacro { get; private set; }

    public bool CanLoadMacro => CanManageLoadedMacrosExternal;

    public bool CanSaveMacro => HasRecordedMacro && CanManageLoadedMacrosExternal;

    public bool IsSelectedOnlyMode
    {
        get => _loadedMacroSession.PlaybackMode is LoadedMacroPlaybackMode.SelectedOnly;
        set
        {
            if (value)
            {
                SetPlaybackMode(LoadedMacroPlaybackMode.SelectedOnly);
            }
        }
    }

    public bool IsAdvanceSelectionMode
    {
        get => _loadedMacroSession.PlaybackMode is LoadedMacroPlaybackMode.AdvanceSelection;
        set
        {
            if (value)
            {
                SetPlaybackMode(LoadedMacroPlaybackMode.AdvanceSelection);
            }
        }
    }

    public bool IsSequentialCycleMode
    {
        get => _loadedMacroSession.PlaybackMode is LoadedMacroPlaybackMode.SequentialCycle;
        set
        {
            if (value)
            {
                SetPlaybackMode(LoadedMacroPlaybackMode.SequentialCycle);
            }
        }
    }

    public bool ShowSequenceRepeatSettings => HasLoadedMacros && IsSequentialCycleMode;

    // Kept manual: StatusChanged must fire after the PropertyChanged notification, a generated OnChanged hook would fire before it.
    public string Status
    {
        get => _status;
        private set
        {
            if (!string.Equals(_status, value, StringComparison.Ordinal))
            {
                _status = value;
                OnPropertyChanged();
                StatusChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Add a macro to the current session and select it.
    /// </summary>
    public void SetMacro(MacroSequence? macro)
    {
        if (macro is null)
        {
            return;
        }

        ApplyPendingNameForNewMacro(macro, treatDefaultPlaceholderAsUnnamed: true);
        _ = _loadedMacroSession.AddMacro(macro);
    }

    /// <summary>
    /// Update a known loaded macro in place when the editor is linked to it.
    /// Falls back to adding a new session item when the link is missing or stale.
    /// </summary>
    public LoadedMacroListItem? UpsertMacro(Guid? sessionId, MacroSequence? macro, string? sourcePath = null)
    {
        if (macro is null)
        {
            return null;
        }

        if (sessionId is not null)
        {
            var updatedItem = _loadedMacroSession.UpdateMacro(sessionId.Value, macro, sourcePath);
            if (updatedItem is not null)
            {
                return updatedItem;
            }
        }

        ApplyPendingNameForNewMacro(macro);
        return _loadedMacroSession.AddMacro(macro, sourcePath);
    }

    /// <summary>
    /// Update the currently selected loaded macro when the caller explicitly targets it.
    /// </summary>
    public void UpsertSelectedMacro(MacroSequence? macro)
    {
        if (macro is null)
        {
            return;
        }

        if (ShouldApplyPendingMacroName(macro.Name))
        {
            macro.Name = MacroName;
        }

        if (_loadedMacroSession.UpdateSelectedMacro(macro))
        {
            return;
        }

        _ = _loadedMacroSession.AddMacro(macro);
    }

    public async Task SaveMacroAsync()
    {
        var currentItem = SelectedMacroItem;
        if (currentItem is null || !CanSaveMacro)
        {
            return;
        }

        var currentMacro = currentItem.Macro;
        var macroNameToSave = currentItem.Name;
        if (currentMacro is null || string.IsNullOrWhiteSpace(macroNameToSave))
        {
            return;
        }

        try
        {
            var filters =
                new[]
                {
                    new FileDialogFilter { Name = _localizationService["Files_OpenMacroDialogFilter"], Extensions = ["macro"] },
                };

            var baseName = macroNameToSave.EndsWith(".macro", StringComparison.OrdinalIgnoreCase)
                ? macroNameToSave[..^6]
                : macroNameToSave;
            var filePath = await _dialogService.ShowSaveFileDialogAsync(_localizationService["Files_SaveDialogTitle"], $"{baseName}.macro", filters).ConfigureAwait(false);

            if (string.IsNullOrEmpty(filePath))
            {
                await RunOnUiThreadAsync(() => SetStatusKind(FilesStatusKind.SaveCancelled)).ConfigureAwait(false);
                return;
            }

            var macroToSave = CreateSaveSnapshot(currentMacro, macroNameToSave);
            await _fileManager.SaveAsync(macroToSave, filePath).ConfigureAwait(false);
            await RunOnUiThreadAsync(() =>
            {
                currentItem.UpdateSourcePath(filePath);
                SetTransientStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Files_StatusSavedTo"], Path.GetFileName(filePath)));
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() => SetTransientStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Files_StatusSaveError"], ex.Message))).ConfigureAwait(false);
        }
    }

    public async Task LoadMacroAsync()
    {
        if (!CanLoadMacro)
        {
            return;
        }

        try
        {
            var filters =
                new[]
                {
                    new FileDialogFilter { Name = _localizationService["Files_OpenMacroDialogFilter"], Extensions = ["macro"] },
                };

            var filePath = await _dialogService.ShowOpenFileDialogAsync(_localizationService["Files_LoadDialogTitle"], filters).ConfigureAwait(false);

            if (string.IsNullOrEmpty(filePath))
            {
                await RunOnUiThreadAsync(() => SetStatusKind(FilesStatusKind.LoadCancelled)).ConfigureAwait(false);
                return;
            }

            var macro = await _fileManager.LoadAsync(filePath).ConfigureAwait(false);
            if (macro is null)
            {
                await RunOnUiThreadAsync(() => SetTransientStatus(_localizationService["Files_StatusLoadUnreadable"])).ConfigureAwait(false);
                return;
            }

            await RunOnUiThreadAsync(() =>
            {
                _ = _loadedMacroSession.AddMacro(macro, filePath);
                SetTransientStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Files_StatusLoaded"], Path.GetFileName(filePath)));
                MacroLoaded?.Invoke(this, macro);
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() => SetTransientStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Files_StatusLoadError"], ex.Message))).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Get current selected macro.
    /// </summary>
    public MacroSequence? CurrentMacro => _loadedMacroSession.SelectedMacro;

    private static MacroSequence CreateSaveSnapshot(MacroSequence macro, string name)
    {
        var snapshot = macro.Clone();
        snapshot.Name = name;
        NormalizeCurrentPositionMouseButtonEvents(snapshot);
        snapshot.IsAbsoluteCoordinates = MacroPositionSemantics.GetCoordinateModeSummary(snapshot) is CoordinateModeSummary.Absolute;
        return snapshot;
    }

    private static void NormalizeCurrentPositionMouseButtonEvents(MacroSequence macro)
    {
        if (macro.Events is null)
        {
            return;
        }

        for (var index = 0; index < macro.Events.Count; index++)
        {
            var ev = macro.Events[index];
            if (!ev.UseCurrentPosition || !MacroPositionSemantics.IsNonScrollMouseButtonEvent(ev))
            {
                continue;
            }

            ev.X = 0;
            ev.Y = 0;
            ev.CoordinateMode = null;
            ev.CoordinateSpace = null;
            macro.Events[index] = ev;
        }
    }

    [RelayCommand]
    private async Task RemoveLoadedMacroAsync(LoadedMacroListItem? item)
    {
        if (item is null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmationAsync(
            _localizationService["Files_DeleteLoadedMacroTitle"],
            string.Format(_localizationService.CurrentCulture, _localizationService["Files_DeleteLoadedMacroMessage"], item.Name)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            if (_loadedMacroSession.RemoveMacro(item))
            {
                SetTransientStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Files_StatusRemoved"], item.Name));
            }
        }).ConfigureAwait(false);
    }

    private void ApplyPendingNameForNewMacro(MacroSequence macro, bool treatDefaultPlaceholderAsUnnamed = false)
    {
        if (!ShouldApplyPendingMacroName(macro.Name, treatDefaultPlaceholderAsUnnamed))
        {
            return;
        }

        if (_loadedMacroSession.SelectedMacroItem is not null)
        {
            return;
        }

        macro.Name = GetPendingMacroNameForNewItem();
    }

    private string GetPendingMacroNameForNewItem()
    {
        return string.IsNullOrWhiteSpace(_macroName)
            ? DefaultMacroName
            : _macroName.Trim();
    }

    private static bool ShouldApplyPendingMacroName(string? macroName, bool treatDefaultPlaceholderAsUnnamed = false)
    {
        if (string.IsNullOrWhiteSpace(macroName) || string.Equals(macroName, MacroNameDefaults.UnnamedMacroName, StringComparison.Ordinal))
        {
            return true;
        }

        return treatDefaultPlaceholderAsUnnamed
            && string.Equals(macroName, DefaultMacroName, StringComparison.Ordinal);
    }

    private void SetPlaybackMode(LoadedMacroPlaybackMode mode)
    {
        if (_loadedMacroSession.PlaybackMode == mode)
        {
            return;
        }

        _loadedMacroSession.PlaybackMode = mode;
    }

    private void OnSelectedMacroChanged(object? sender, EventArgs e)
    {
        PostToUiThread(() =>
        {
            SyncFromSelectedMacro();
            SelectedMacroChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnSelectedMacroUpdated(object? sender, EventArgs e)
    {
        PostToUiThread(() =>
        {
            SyncFromSelectedMacro();
            SelectedMacroUpdated?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnPlaybackModeChanged(object? sender, EventArgs e)
    {
        PostToUiThread(() =>
        {
            OnPropertyChanged(nameof(IsSelectedOnlyMode));
            OnPropertyChanged(nameof(IsAdvanceSelectionMode));
            OnPropertyChanged(nameof(IsSequentialCycleMode));
            OnPropertyChanged(nameof(ShowSequenceRepeatSettings));
        });
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        PostToUiThread(() =>
        {
            foreach (var item in LoadedMacros)
            {
                item.RefreshLocalizedProperties();
            }

            OnPropertyChanged(nameof(LoadedMacros));
            OnPropertyChanged(nameof(SelectedMacroItem));
            OnPropertyChanged(nameof(MacroName));

            if (_statusKind is FilesStatusKind.Ready or FilesStatusKind.LoadCancelled or FilesStatusKind.SaveCancelled)
            {
                Status = BuildStatus(_statusKind);
            }
        });
    }

    private void SyncFromSelectedMacro()
    {
        var currentMacro = CurrentMacro;
        if (currentMacro is not null && !string.IsNullOrWhiteSpace(currentMacro.Name))
        {
            _macroName = currentMacro.Name;
        }
        else
        {
            _macroName = DefaultMacroName;
        }

        HasRecordedMacro = MacroPlayableActionCounter.HasPlayableActions(currentMacro);
        OnPropertyChanged(nameof(LoadedMacros));
        OnPropertyChanged(nameof(SelectedMacroItem));
        OnPropertyChanged(nameof(SelectedSequenceRepeatCount));
        OnPropertyChanged(nameof(HasLoadedMacros));
        OnPropertyChanged(nameof(MacroName));
        OnPropertyChanged(nameof(ShowSequenceRepeatSettings));
    }

    private void SetStatusKind(FilesStatusKind statusKind)
    {
        _statusKind = statusKind;
        Status = BuildStatus(statusKind);
    }

    private void SetTransientStatus(string status)
    {
        _statusKind = FilesStatusKind.Other;
        Status = status;
    }

    private string BuildStatus(FilesStatusKind statusKind)
    {
        return statusKind switch
        {
            FilesStatusKind.Ready => _localizationService["Files_StatusReady"],
            FilesStatusKind.LoadCancelled => _localizationService["Files_StatusLoadCancelled"],
            FilesStatusKind.SaveCancelled => _localizationService["Files_StatusSaveCancelled"],
            FilesStatusKind.Other => _status,
            _ => throw new ArgumentOutOfRangeException(nameof(statusKind), statusKind, message: null),
        };
    }
}
