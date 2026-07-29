
namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Shortcuts tab - manages shortcut-triggered macro tasks
/// </summary>
public partial class ShortcutViewModel : ViewModelBase, IDisposable
{
    private readonly IShortcutService _shortcutService;
    private readonly IDialogService _dialogService;
    private readonly IManageShortcut? _manageShortcut;
    private bool _disposed;
    private readonly Dictionary<Guid, ShortcutTaskEditor> _editors = [];

    public ObservableCollection<ShortcutTaskEditor> Tasks { get; } = [];

    public IGlobalHotkeyService GlobalHotkeyService { get; }

    public ILocalizationService LocalizationService { get; }

    public Task InitializationTask { get; }

    public string TaskCountText => string.Format(LocalizationService.CurrentCulture, LocalizationService["Shortcut_ItemsText"], Tasks.Count);

    public ShortcutTaskEditor? SelectedTask
    {
        get; set
        {
            if (field != value)
            {
                field?.PropertyChanged -= OnSelectedTaskPropertyChanged;

                field = value;
                field?.PropertyChanged += OnSelectedTaskPropertyChanged;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedTask));
                OnPropertyChanged(nameof(SelectedMacroFilePath));
                OnPropertyChanged(nameof(SelectedMacroFileName));
                OnPropertyChanged(nameof(SelectedHotkeyString));
                OnSelectedTaskStatusChanged();
            }
        }
    }

    private void OnSelectedTaskPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShortcutTaskEditor.LastTriggeredTime) or nameof(ShortcutTaskEditor.LastStatus))
        {
            PostToUiThread(OnSelectedTaskStatusChanged);
        }
    }

    public bool HasSelectedTask => SelectedTask is not null;

    public string? SelectedMacroFilePath
    {
        get => string.IsNullOrEmpty(SelectedTask?.MacroFilePath) ? null : SelectedTask.MacroFilePath;
        set
        {
            if (SelectedTask is not null && !string.Equals(SelectedTask.MacroFilePath, value ?? "", StringComparison.Ordinal))
            {
                SelectedTask.MacroFilePath = value ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedMacroFileName));
                OnPropertyChanged(nameof(SelectedTask));
            }
        }
    }

    public string SelectedMacroFileName =>
        string.IsNullOrEmpty(SelectedTask?.MacroFilePath)
            ? LocalizationService["Shortcut_NoFileSelected"]
            : Path.GetFileName(SelectedTask.MacroFilePath);

    public string SelectedHotkeyString
    {
        get => SelectedTask?.HotkeyString ?? "";
        set
        {
            if (SelectedTask is not null && !string.Equals(SelectedTask.HotkeyString, value, StringComparison.Ordinal))
            {
                SelectedTask.HotkeyString = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedTask));
            }
        }
    }

    public string SelectedLastTriggeredText => SelectedTask?.LastTriggeredTime?.ToLocalTime().ToString("G", LocalizationService.CurrentCulture)
        ?? LocalizationService["Shortcut_Never"];

    public string SelectedStatusText => string.IsNullOrWhiteSpace(SelectedTask?.LastStatus)
        ? LocalizationService["Shortcut_StatusPlaceholder"]
        : SelectedTask.LastStatus;

    // Events for global status
    public event EventHandler<string>? StatusChanged;

    public ShortcutViewModel(
        IShortcutService shortcutService,
        IDialogService dialogService,
        IGlobalHotkeyService hotkeyService,
        ILocalizationService localizationService)
    {
        _shortcutService = shortcutService;
        _dialogService = dialogService;
        GlobalHotkeyService = hotkeyService;
        LocalizationService = localizationService;
        LocalizationService.CultureChanged += OnCultureChanged;

        // Subscribe to shortcut execution events
        _shortcutService.ShortcutStarting += OnShortcutStarting;
        _shortcutService.ShortcutExecuted += OnShortcutExecuted;
        _shortcutService.Tasks?.CollectionChanged += OnTasksCollectionChanged;
        RemapEditors();

        // Load saved shortcuts and start listening
        InitializationTask = InitializeAsyncSafeAsync();
    }

    public ShortcutViewModel(
        IManageShortcut manageShortcut,
        IShortcutService shortcutService,
        IDialogService dialogService,
        IGlobalHotkeyService hotkeyService,
        ILocalizationService localizationService)
        : this(shortcutService, dialogService, hotkeyService, localizationService)
    {
        _manageShortcut = manageShortcut;
    }

    private async Task InitializeAsyncSafeAsync()
    {
        try
        {
            await _shortcutService.LoadAsync().ConfigureAwait(false);
            _shortcutService.Start();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[ShortcutViewModel] Failed to initialize shortcuts");
            var status = string.Format(LocalizationService.CurrentCulture, LocalizationService["Shortcut_StatusInitFailed"], ex.Message);
            await RunOnUiThreadAsync(() => RaiseStatus(status)).ConfigureAwait(false);
        }
    }

    public void RefreshProfileData()
    {
        RemapEditors();
        SelectedTask = Tasks.FirstOrDefault();
        OnPropertyChanged(nameof(Tasks));
        OnPropertyChanged(nameof(TaskCountText));
        OnPropertyChanged(nameof(SelectedTask));
        OnPropertyChanged(nameof(SelectedMacroFileName));
        OnPropertyChanged(nameof(SelectedHotkeyString));
        OnSelectedTaskStatusChanged();
    }

    [RelayCommand]
    private async Task AddTaskAsync()
    {
        var task = new ShortcutTask
        {
            Name = string.Format(LocalizationService.CurrentCulture, LocalizationService["Shortcut_DefaultTaskName"], Tasks.Count + 1),
        };
        if (_manageShortcut is not null)
        {
            _ = await _manageShortcut.AddAsync(task, default).ConfigureAwait(false);
        }
        else
        {
            _shortcutService.AddTask(task);
        }
        await RunOnUiThreadAsync(() =>
        {
            RemapEditors();
            if (!_editors.TryGetValue(task.Id, out var editor))
            {
                editor = new ShortcutTaskEditor();
                editor.Load(task);
                _editors[task.Id] = editor;
                Tasks.Add(editor);
            }
            SelectedTask = editor;
            OnPropertyChanged(nameof(TaskCountText));
        }).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RemoveTaskAsync(ShortcutTaskEditor? task)
    {
        if (task is null)
        {
            return;
        }

        var coreTask = _shortcutService.Tasks.FirstOrDefault(candidate => candidate.Id == task.Id) ?? task.ToCore();
        var confirmed = await _dialogService.ShowConfirmationAsync(
            LocalizationService["Shortcut_DeleteTitle"],
            string.Format(LocalizationService.CurrentCulture, LocalizationService["Shortcut_DeleteMessage"], task.Name)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        if (_manageShortcut is not null)
        {
            var selectedTaskId = SelectedTask?.Id;
            _ = await _manageShortcut.RemoveAsync(new TaskRequest(task.Id), default).ConfigureAwait(false);
            await RunOnUiThreadAsync(() =>
            {
                RemapEditors();
                SelectedTask = selectedTaskId is Guid id
                    ? Tasks.FirstOrDefault(candidate => candidate.Id == id) ?? Tasks.FirstOrDefault()
                    : Tasks.FirstOrDefault();
                OnPropertyChanged(nameof(TaskCountText));
            }).ConfigureAwait(false);
            return;
        }

        var wasSelected = SelectedTask?.Id == task.Id;
        await RunOnUiThreadAsync(() =>
        {
            _shortcutService.RemoveTask(task.Id);
            if (wasSelected)
            {
                SelectedTask = Tasks.FirstOrDefault();
            }
        }).ConfigureAwait(false);
        await SaveChangesAsync(showSuccessStatus: false, rollback: () =>
        {
            _shortcutService.AddTask(coreTask);
            RemapEditors();
            if (wasSelected)
            {
                SelectedTask = task;
            }
        }).ConfigureAwait(false);
    }

    [RelayCommand]
    private void SelectTask(ShortcutTaskEditor? task)
    {
        if (task is not null)
        {
            SelectedTask = SelectedTask?.Id == task.Id ? null : task;
        }
    }

    [RelayCommand]
    private async Task BrowseMacroAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var filters = new FileDialogFilter[]
        {
            new FileDialogFilter { Name = LocalizationService["Shortcut_OpenMacroDialogFilter"], Extensions = ["macro"] },
        };

        var filePath = await _dialogService.ShowOpenFileDialogAsync(
            LocalizationService["Shortcut_OpenMacroDialogTitle"],
            filters).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(filePath))
        {
            await RunOnUiThreadAsync(() => SelectedMacroFilePath = filePath).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await SaveChangesAsync(showSuccessStatus: true).ConfigureAwait(false);
    }

    private async Task SaveChangesAsync(bool showSuccessStatus, Action? rollback = null)
    {
        try
        {
            if (_manageShortcut is not null && SelectedTask is not null)
            {
                var core = SelectedTask.ToCore();
                _ = await _manageShortcut.UpdateAsync(core, default).ConfigureAwait(false);
                await RunOnUiThreadAsync(RemapEditors).ConfigureAwait(false);
            }
            else if (SelectedTask is { } selectedTask)
            {
                await RunOnUiThreadAsync(() =>
                {
                    selectedTask.ApplyToCore(_shortcutService.Tasks.First(task => task.Id == selectedTask.Id));
                }).ConfigureAwait(false);
            }
            await _shortcutService.SaveAsync().ConfigureAwait(false);
            if (showSuccessStatus)
            {
                await RunOnUiThreadAsync(() => RaiseStatus(LocalizationService["Shortcut_StatusChangesSaved"])).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[ShortcutViewModel] Failed to save shortcut tasks");
            var status = string.Format(LocalizationService.CurrentCulture, LocalizationService["Shortcut_StatusSaveFailed"], ex.Message);
            await RunOnUiThreadAsync(() =>
            {
                rollback?.Invoke();
                RaiseStatus(status);
            }).ConfigureAwait(false);
            try
            {
                await _dialogService.ShowMessageAsync(LocalizationService["Shortcut_SaveFailedTitle"], status).ConfigureAwait(false);
            }
            catch (Exception dialogEx) when (dialogEx is not OutOfMemoryException)
            {
                Log.Warning(dialogEx, "[ShortcutViewModel] Failed to show save error dialog");
            }
        }
    }

    public void OnHotkeyChanged(string newHotkey)
    {
        SelectedHotkeyString = newHotkey;
    }

    [RelayCommand]
    private async Task TaskEnabledChangedAsync(ShortcutTaskEditor task)
    {
        var previousEnabled = !task.IsEnabled;
        var coreTask = _shortcutService.Tasks.FirstOrDefault(candidate => candidate.Id == task.Id);
        if (coreTask is null)
        {
            return;
        }

        task.ApplyToCore(coreTask);
        if (_manageShortcut is not null)
        {
            var selectedTaskId = SelectedTask?.Id;
            try
            {
                _ = await _manageShortcut.SetEnabledAsync(new TaskRequest(task.Id, task.IsEnabled), default).ConfigureAwait(false);
                await RunOnUiThreadAsync(RemapEditors).ConfigureAwait(false);
            }
            finally
            {
                await RunOnUiThreadAsync(() =>
                {
                    SelectedTask = selectedTaskId is Guid id
                        ? Tasks.FirstOrDefault(candidate => candidate.Id == id)
                        : null;
                }).ConfigureAwait(false);
            }
            return;
        }

        _shortcutService.SetTaskEnabled(task.Id, task.IsEnabled);
        await SaveChangesAsync(showSuccessStatus: false, rollback: () => _shortcutService.SetTaskEnabled(task.Id, previousEnabled)).ConfigureAwait(false);
    }

    private void OnShortcutStarting(object? sender, ShortcutStartingEventArgs e)
    {
        var task = e.Task;
        Dispatcher.UIThread.Post(() =>
        {
            RaiseStatus(string.Format(LocalizationService.CurrentCulture, LocalizationService["Shortcut_StatusRunning"], task.Name));

            SyncRuntimeStatus(task);
            if (SelectedTask?.Id == task.Id)
            {
                OnPropertyChanged(nameof(SelectedTask));
                OnSelectedTaskStatusChanged();
            }
        });
    }

    private void OnShortcutExecuted(object? sender, ShortcutExecutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var statusText = e.Success
                ? string.Format(LocalizationService.CurrentCulture, LocalizationService["Shortcut_StatusCompleted"], e.Task.Name)
                : string.Format(LocalizationService.CurrentCulture, LocalizationService["Shortcut_StatusFailed"], e.Task.Name, e.Message);
            RaiseStatus(statusText);

            SyncRuntimeStatus(e.Task);
            if (SelectedTask?.Id == e.Task.Id)
            {
                OnPropertyChanged(nameof(SelectedTask));
                OnSelectedTaskStatusChanged();
            }
        });
    }

    private void OnSelectedTaskStatusChanged()
    {
        OnPropertyChanged(nameof(SelectedLastTriggeredText));
        OnPropertyChanged(nameof(SelectedStatusText));
    }

    private void OnTasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        PostToUiThread(() =>
        {
            RemapEditors();
            OnPropertyChanged(nameof(TaskCountText));
        });
    }

    private void RemapEditors()
    {
        var tasks = _shortcutService.Tasks ?? [];
        var current = _editors.Keys.ToList();
        foreach (var id in current.Where(id => !tasks.Any(task => task.Id == id)))
        {
            _ = _editors.Remove(id);
        }

        foreach (var task in tasks)
        {
            if (!_editors.TryGetValue(task.Id, out var editor))
            {
                editor = new ShortcutTaskEditor();
                _editors[task.Id] = editor;
            }
            editor.Load(task);
        }
        Tasks.Clear();
        foreach (var editor in tasks.Select(task => _editors[task.Id]))
        {
            Tasks.Add(editor);
        }
    }

    private void SyncRuntimeStatus(ShortcutTask task)
    {
        if (_editors.TryGetValue(task.Id, out var editor))
        {
            editor.SyncRuntimeStatus(task.LastTriggeredTime, task.LastStatus);
        }
    }

    private void RaiseStatus(string message)
    {
        if (Avalonia.Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            StatusChanged?.Invoke(this, message);
            return;
        }

        Dispatcher.UIThread.Post(() => StatusChanged?.Invoke(this, message));
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        PostToUiThread(() =>
        {
            OnPropertyChanged(nameof(TaskCountText));
            OnPropertyChanged(nameof(SelectedMacroFileName));
            OnPropertyChanged(nameof(SelectedTask));
            OnSelectedTaskStatusChanged();
        });
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _shortcutService.ShortcutStarting -= OnShortcutStarting;
        _shortcutService.ShortcutExecuted -= OnShortcutExecuted;
        _shortcutService.Tasks?.CollectionChanged -= OnTasksCollectionChanged;
        SelectedTask?.PropertyChanged -= OnSelectedTaskPropertyChanged;
        LocalizationService.CultureChanged -= OnCultureChanged;
    }
}
