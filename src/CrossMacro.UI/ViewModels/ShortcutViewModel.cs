
namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Shortcuts tab - manages shortcut-triggered macro tasks
/// </summary>
public partial class ShortcutViewModel : ViewModelBase, IDisposable
{
    private readonly IShortcutService _shortcutService;
    private readonly IDialogService _dialogService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ILocalizationService _localizationService;
    private readonly IManageShortcut? _manageShortcut;
    private ShortcutTaskEditor? _selectedTask;
    private bool _disposed;
    private readonly Dictionary<Guid, ShortcutTaskEditor> _editors = [];

    public ObservableCollection<ShortcutTaskEditor> Tasks { get; } = [];

    public IGlobalHotkeyService GlobalHotkeyService => _hotkeyService;

    public ILocalizationService LocalizationService => _localizationService;

    public Task InitializationTask { get; }

    public string TaskCountText => string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_ItemsText"], Tasks.Count);

    public ShortcutTaskEditor? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (_selectedTask != value)
            {
                if (_selectedTask is not null)
                {
                    _selectedTask.PropertyChanged -= OnSelectedTaskPropertyChanged;
                }

                _selectedTask = value;
                if (_selectedTask is not null)
                {
                    _selectedTask.PropertyChanged += OnSelectedTaskPropertyChanged;
                }
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
            RaiseOnUiThread(OnSelectedTaskStatusChanged);
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
            ? _localizationService["Shortcut_NoFileSelected"]
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

    public string SelectedLastTriggeredText => SelectedTask?.LastTriggeredTime?.ToLocalTime().ToString("G", _localizationService.CurrentCulture)
        ?? _localizationService["Shortcut_Never"];

    public string SelectedStatusText => string.IsNullOrWhiteSpace(SelectedTask?.LastStatus)
        ? _localizationService["Shortcut_StatusPlaceholder"]
        : SelectedTask.LastStatus!;

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
        _hotkeyService = hotkeyService;
        _localizationService = localizationService;
        _localizationService.CultureChanged += OnCultureChanged;

        // Subscribe to shortcut execution events
        _shortcutService.ShortcutStarting += OnShortcutStarting;
        _shortcutService.ShortcutExecuted += OnShortcutExecuted;
        _shortcutService.Tasks?.CollectionChanged += OnTasksCollectionChanged;
        RemapEditors();

        // Load saved shortcuts and start listening
        InitializationTask = InitializeAsyncSafe();
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

    private async Task InitializeAsyncSafe()
    {
        try
        {
            await _shortcutService.LoadAsync().ConfigureAwait(false);
            _shortcutService.Start();
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "[ShortcutViewModel] Failed to initialize shortcuts");
            RaiseStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_StatusInitFailed"], ex.Message));
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
            Name = string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_DefaultTaskName"], Tasks.Count + 1),
        };
        if (_manageShortcut is not null)
        {
            await _manageShortcut.AddAsync(task).ConfigureAwait(false);
        }
        else
        {
            _shortcutService.AddTask(task);
        }
        RemapEditors();
        SelectedTask = _editors[task.Id];
        OnPropertyChanged(nameof(TaskCountText));
    }

    [RelayCommand]
    private async Task RemoveTaskAsync(ShortcutTaskEditor? task)
    {
        if (task is null)
        {
            return;
        }

        var coreTask = task.ToCore();
        var confirmed = await _dialogService.ShowConfirmationAsync(
            _localizationService["Shortcut_DeleteTitle"],
            string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_DeleteMessage"], task.Name)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        if (_manageShortcut is not null)
        {
            var selectedTaskId = SelectedTask?.Id;
            await _manageShortcut.RemoveAsync(new TaskRequest(task.Id)).ConfigureAwait(false);
            RemapEditors();
            SelectedTask = selectedTaskId is Guid id
                ? Tasks.FirstOrDefault(candidate => candidate.Id == id) ?? Tasks.FirstOrDefault()
                : Tasks.FirstOrDefault();
            OnPropertyChanged(nameof(TaskCountText));
            return;
        }

        var wasSelected = SelectedTask?.Id == task.Id;
        _shortcutService.RemoveTask(task.Id);
        if (wasSelected)
        {
            SelectedTask = Tasks.FirstOrDefault();
        }
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
            new FileDialogFilter { Name = _localizationService["Shortcut_OpenMacroDialogFilter"], Extensions = new[] { "macro" } },
        };

        var filePath = await _dialogService.ShowOpenFileDialogAsync(
            _localizationService["Shortcut_OpenMacroDialogTitle"],
            filters).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(filePath))
        {
            SelectedMacroFilePath = filePath;
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
                await _manageShortcut.UpdateAsync(core).ConfigureAwait(false);
                RemapEditors();
            }
            else
            {
                if (SelectedTask is not null)
                {
                    SelectedTask.ApplyToCore(_shortcutService.Tasks.First(task => task.Id == SelectedTask.Id));
                }
                await _shortcutService.SaveAsync().ConfigureAwait(false);
            }
            if (showSuccessStatus)
            {
                RaiseStatus(_localizationService["Shortcut_StatusChangesSaved"]);
            }
        }
        catch (Exception ex)
        {
            rollback?.Invoke();
            Log.LogError(ex, "[ShortcutViewModel] Failed to save shortcut tasks");
            var status = string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_StatusSaveFailed"], ex.Message);
            RaiseStatus(status);
            try
            {
                await _dialogService.ShowMessageAsync(_localizationService["Shortcut_SaveFailedTitle"], status).ConfigureAwait(false);
            }
            catch (Exception dialogEx)
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
                await _manageShortcut.SetEnabledAsync(new TaskRequest(task.Id, task.IsEnabled)).ConfigureAwait(false);
                RemapEditors();
            }
            finally
            {
                SelectedTask = selectedTaskId is Guid id
                    ? Tasks.FirstOrDefault(candidate => candidate.Id == id)
                    : null;
            }
            return;
        }

        if (_manageShortcut is null)
        {
            _shortcutService.SetTaskEnabled(task.Id, task.IsEnabled);
        }
        await SaveChangesAsync(showSuccessStatus: false, rollback: () => _shortcutService.SetTaskEnabled(task.Id, previousEnabled)).ConfigureAwait(false);
    }

    private void OnShortcutStarting(object? sender, ShortcutStartingEventArgs e)
    {
        var task = e.Task;
        Dispatcher.UIThread.Post(() =>
        {
            RaiseStatus(string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_StatusRunning"], task.Name));

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
                ? string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_StatusCompleted"], e.Task.Name)
                : string.Format(_localizationService.CurrentCulture, _localizationService["Shortcut_StatusFailed"], e.Task.Name, e.Message);
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
        RaiseOnUiThread(RemapEditors);
        OnPropertyChanged(nameof(TaskCountText));
    }

    private void RemapEditors()
    {
        var current = _editors.Keys.ToList();
        foreach (var id in current.Where(id => !_shortcutService.Tasks.Any(task => task.Id == id)))
        {
            _editors.Remove(id);
        }

        foreach (var task in _shortcutService.Tasks)
        {
            if (!_editors.TryGetValue(task.Id, out var editor))
            {
                editor = new ShortcutTaskEditor();
                _editors[task.Id] = editor;
            }
            editor.Load(task);
        }
        Tasks.Clear();
        foreach (var editor in _shortcutService.Tasks.Select(task => _editors[task.Id]))
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

    private static void RaiseOnUiThread(Action action)
    {
        if (Avalonia.Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
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
        OnPropertyChanged(nameof(TaskCountText));
        OnPropertyChanged(nameof(SelectedMacroFileName));
        OnPropertyChanged(nameof(SelectedTask));
        OnSelectedTaskStatusChanged();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _shortcutService.ShortcutStarting -= OnShortcutStarting;
        _shortcutService.ShortcutExecuted -= OnShortcutExecuted;
        _shortcutService.Tasks?.CollectionChanged -= OnTasksCollectionChanged;
        if (_selectedTask is not null)
        {
            _selectedTask.PropertyChanged -= OnSelectedTaskPropertyChanged;
        }
        _localizationService.CultureChanged -= OnCultureChanged;
    }
}
