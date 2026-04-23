using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Models;
using CrossMacro.UI.Services;

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
        Other
    }

    private const string DefaultMacroName = MacroNameDefaults.NewRecordedMacroName;
    private const int DefaultSequenceRepeatCount = 1;

    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    private readonly ILoadedMacroSession _loadedMacroSession;
    private readonly ILocalizationService _localizationService;

    private string _macroName = DefaultMacroName;
    private bool _hasRecordedMacro;
    private string _status;
    private bool _canManageLoadedMacrosExternal = true;
    private FilesStatusKind _statusKind = FilesStatusKind.Ready;

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

    public string MacroName
    {
        get => _loadedMacroSession.SelectedMacroItem?.Name ?? _macroName;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? DefaultMacroName : value.Trim();
            var selectedItem = _loadedMacroSession.SelectedMacroItem;
            if (selectedItem != null)
            {
                if (selectedItem.Name == normalized)
                {
                    return;
                }

                _loadedMacroSession.RenameSelected(normalized);
                OnPropertyChanged();
                return;
            }

            if (_macroName == normalized)
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
            if (selectedItem == null)
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

    public bool HasRecordedMacro
    {
        get => _hasRecordedMacro;
        private set
        {
            if (_hasRecordedMacro != value)
            {
                _hasRecordedMacro = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSaveMacro));
            }
        }
    }

    public bool CanManageLoadedMacrosExternal
    {
        get => _canManageLoadedMacrosExternal;
        set
        {
            if (_canManageLoadedMacrosExternal == value)
            {
                return;
            }

            _canManageLoadedMacrosExternal = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanLoadMacro));
            OnPropertyChanged(nameof(CanSaveMacro));
        }
    }

    public bool CanLoadMacro => CanManageLoadedMacrosExternal;

    public bool CanSaveMacro => HasRecordedMacro && CanManageLoadedMacrosExternal;

    public bool IsSelectedOnlyMode
    {
        get => _loadedMacroSession.PlaybackMode == LoadedMacroPlaybackMode.SelectedOnly;
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
        get => _loadedMacroSession.PlaybackMode == LoadedMacroPlaybackMode.AdvanceSelection;
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
        get => _loadedMacroSession.PlaybackMode == LoadedMacroPlaybackMode.SequentialCycle;
        set
        {
            if (value)
            {
                SetPlaybackMode(LoadedMacroPlaybackMode.SequentialCycle);
            }
        }
    }

    public bool ShowSequenceRepeatSettings => HasLoadedMacros && IsSequentialCycleMode;

    public string Status
    {
        get => _status;
        private set
        {
            if (_status != value)
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
        if (macro == null)
        {
            return;
        }

        ApplyPendingNameForNewMacro(macro, treatDefaultPlaceholderAsUnnamed: true);
        _loadedMacroSession.AddMacro(macro);
    }

    /// <summary>
    /// Update a known loaded macro in place when the editor is linked to it.
    /// Falls back to adding a new session item when the link is missing or stale.
    /// </summary>
    public LoadedMacroListItem? UpsertMacro(Guid? sessionId, MacroSequence? macro, string? sourcePath = null)
    {
        if (macro == null)
        {
            return null;
        }

        if (sessionId.HasValue)
        {
            var updatedItem = _loadedMacroSession.UpdateMacro(sessionId.Value, macro, sourcePath);
            if (updatedItem != null)
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
        if (macro == null)
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

        _loadedMacroSession.AddMacro(macro);
    }

    public async Task SaveMacroAsync()
    {
        var currentItem = SelectedMacroItem;
        if (currentItem == null || !CanSaveMacro)
        {
            return;
        }

        var currentMacro = currentItem.Macro;
        var macroNameToSave = currentItem.Name;
        if (currentMacro == null || string.IsNullOrWhiteSpace(macroNameToSave))
        {
            return;
        }

        try
        {
            var filters =
                new[]
                {
                    new FileDialogFilter { Name = _localizationService["Files_OpenMacroDialogFilter"], Extensions = new[] { "macro" } }
                };

            var baseName = macroNameToSave.EndsWith(".macro", StringComparison.OrdinalIgnoreCase)
                ? macroNameToSave[..^6]
                : macroNameToSave;
            var filePath = await _dialogService.ShowSaveFileDialogAsync(_localizationService["Files_SaveDialogTitle"], $"{baseName}.macro", filters);

            if (string.IsNullOrEmpty(filePath))
            {
                SetStatusKind(FilesStatusKind.SaveCancelled);
                return;
            }

            currentMacro.Name = macroNameToSave;
            await _fileManager.SaveAsync(currentMacro, filePath);
            currentItem.UpdateSourcePath(filePath);

            SetTransientStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Files_StatusSavedTo"], Path.GetFileName(filePath)));
        }
        catch (Exception ex)
        {
            SetTransientStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Files_StatusSaveError"], ex.Message));
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
                    new FileDialogFilter { Name = _localizationService["Files_OpenMacroDialogFilter"], Extensions = new[] { "macro" } }
                };

            var filePath = await _dialogService.ShowOpenFileDialogAsync(_localizationService["Files_LoadDialogTitle"], filters);

            if (string.IsNullOrEmpty(filePath))
            {
                SetStatusKind(FilesStatusKind.LoadCancelled);
                return;
            }

            var macro = await _fileManager.LoadAsync(filePath);
            if (macro == null)
            {
                SetTransientStatus(_localizationService["Files_StatusLoadUnreadable"]);
                return;
            }

            _loadedMacroSession.AddMacro(macro, filePath);
            SetTransientStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Files_StatusLoaded"], Path.GetFileName(filePath)));
            MacroLoaded?.Invoke(this, macro);
        }
        catch (Exception ex)
        {
            SetTransientStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Files_StatusLoadError"], ex.Message));
        }
    }

    /// <summary>
    /// Get current selected macro.
    /// </summary>
    public MacroSequence? GetCurrentMacro() => _loadedMacroSession.SelectedMacro;

    [RelayCommand]
    private async Task RemoveLoadedMacroAsync(LoadedMacroListItem? item)
    {
        if (item == null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmationAsync(
            _localizationService["Files_DeleteLoadedMacroTitle"],
            string.Format(_localizationService.CurrentCulture, _localizationService["Files_DeleteLoadedMacroMessage"], item.Name));

        if (!confirmed)
        {
            return;
        }

        if (_loadedMacroSession.RemoveMacro(item))
        {
            SetTransientStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Files_StatusRemoved"], item.Name));
        }
    }

    private void ApplyPendingNameForNewMacro(MacroSequence macro, bool treatDefaultPlaceholderAsUnnamed = false)
    {
        if (!ShouldApplyPendingMacroName(macro.Name, treatDefaultPlaceholderAsUnnamed))
        {
            return;
        }

        if (_loadedMacroSession.SelectedMacroItem != null)
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
        SyncFromSelectedMacro();
        SelectedMacroChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectedMacroUpdated(object? sender, EventArgs e)
    {
        SyncFromSelectedMacro();
        SelectedMacroUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void OnPlaybackModeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsSelectedOnlyMode));
        OnPropertyChanged(nameof(IsAdvanceSelectionMode));
        OnPropertyChanged(nameof(IsSequentialCycleMode));
        OnPropertyChanged(nameof(ShowSequenceRepeatSettings));
    }

    private void OnCultureChanged(object? sender, EventArgs e)
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
    }

    private void SyncFromSelectedMacro()
    {
        var currentMacro = GetCurrentMacro();
        if (currentMacro != null && !string.IsNullOrWhiteSpace(currentMacro.Name))
        {
            _macroName = currentMacro.Name;
        }
        else
        {
            _macroName = DefaultMacroName;
        }

        HasRecordedMacro = (currentMacro?.Events?.Count ?? 0) > 0;
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
            _ => _status
        };
    }
}
