
namespace CrossMacro.UI.ViewModels.Automation;

/// <summary>
/// ViewModel for the Shortcuts tab - manages shortcut-triggered macro tasks
/// </summary>
public partial class ShortcutViewModel : ViewModelBase, IDisposable
{
    private readonly IShortcutService _shortcutService;
    private readonly IDialogService _dialogService;
    private readonly IManageShortcut _manageShortcut;
    private readonly IWindowManager? _windowManager;
    private bool _disposed;
    private readonly ScopedTaskProjection<ShortcutTask, ShortcutTaskEditor> _projection;

    public ObservableCollection<ShortcutTaskEditor> Tasks => _projection.Items;

    public IGlobalHotkeyService GlobalHotkeyService { get; }

    public ILocalizationService LocalizationService { get; }

    public Task InitializationTask { get; private set; } = Task.CompletedTask;
    private readonly Lock _initializationGate = new();
    private bool _initializationStarted;

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
        IManageShortcut manageShortcut,
        IShortcutService shortcutService,
        IDialogService dialogService,
        IGlobalHotkeyService hotkeyService,
        ILocalizationService localizationService,
        IProfileRuntimeState? profileRuntimeState = null,
        IWindowManager? windowManager = null,
        IUiDispatcher? uiDispatcher = null)
        : base(uiDispatcher)
    {
        _manageShortcut = manageShortcut ?? throw new ArgumentNullException(nameof(manageShortcut));
        _shortcutService = shortcutService ?? throw new ArgumentNullException(nameof(shortcutService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        GlobalHotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        LocalizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _projection = new ScopedTaskProjection<ShortcutTask, ShortcutTaskEditor>(
            _manageShortcut.ListAsync, UiDispatcher, static task => task.Id,
            static scope => new ShortcutTaskEditor { ScopeGeneration = scope },
            static (editor, task) => editor.Load(task),
            () => SelectedTask, selected => SelectedTask = selected,
            () => { OnPropertyChanged(nameof(Tasks)); OnPropertyChanged(nameof(TaskCountText)); });
        _ = profileRuntimeState; // Initialization now uses a scoped Application snapshot.
        _windowManager = windowManager;
        LocalizationService.CultureChanged += OnCultureChanged;

        // Subscribe to shortcut execution events
        _shortcutService.ShortcutStarting += OnShortcutStarting;
        _shortcutService.ShortcutExecuted += OnShortcutExecuted;
        _shortcutService.Tasks?.CollectionChanged += OnTasksCollectionChanged;

        // Load saved shortcuts and start listening

    }



    public Task InitializeAsync()
    {
        lock (_initializationGate)
        {
            if (!_initializationStarted)
            {
                _initializationStarted = true;
                InitializationTask = InitializeAsyncSafeAsync();
            }
            return InitializationTask;
        }
    }

    private async Task InitializeAsyncSafeAsync()
    {
        try
        {
            await RefreshEditorsAsync(propagateError: true).ConfigureAwait(false);
            await RunOnUiThreadAsync(() => { if (!_disposed) { SelectedTask = Tasks.FirstOrDefault(); } }).ConfigureAwait(false);
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
        if (_disposed) { return; }
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
        var scope = _projection.ScopeGeneration;
        var task = new ShortcutTask
        {
            Name = string.Format(LocalizationService.CurrentCulture, LocalizationService["Shortcut_DefaultTaskName"], Tasks.Count + 1),
        };
        await PersistMutationAsync(async () => { _ = await _manageShortcut.AddAsync(task, scope, CancellationToken.None).ConfigureAwait(false); }, selectTaskId: task.Id).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RemoveTaskAsync(ShortcutTaskEditor? task)
    {
        if (task is null)
        {
            return;
        }

        var scope = task.ScopeGeneration;
        var confirmed = await _dialogService.ShowConfirmationAsync(
            LocalizationService["Shortcut_DeleteTitle"],
            string.Format(LocalizationService.CurrentCulture, LocalizationService["Shortcut_DeleteMessage"], task.Name)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        await PersistMutationAsync(async () => { _ = await _manageShortcut.RemoveAsync(new TaskRequest(task.Id, ExpectedScopeGeneration: scope), CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
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
        var selectedTask = SelectedTask;
        if (selectedTask is null)
        {
            return;
        }

        var scope = selectedTask.ScopeGeneration;
        var filters = new FileDialogFilter[]
        {
            new FileDialogFilter { Name = LocalizationService["Shortcut_OpenMacroDialogFilter"], Extensions = ["macro"] },
        };

        var filePath = await _dialogService.ShowOpenFileDialogAsync(
            LocalizationService["Shortcut_OpenMacroDialogTitle"],
            filters).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(filePath))
        {
            await RunOnUiThreadAsync(() =>
            {
                if (_disposed || scope != _projection.ScopeGeneration || !ReferenceEquals(SelectedTask, selectedTask))
                { RaiseStatus("The active profile or selected task changed. Reopen the file picker and try again."); return; }
                selectedTask.MacroFilePath = filePath;
                OnPropertyChanged(nameof(SelectedMacroFilePath));
                OnPropertyChanged(nameof(SelectedMacroFileName));
                OnPropertyChanged(nameof(SelectedTask));
            }).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private void AddWindowRule()
    {
        SelectedTask?.AddWindowRule();
    }

    [RelayCommand]
    private void RemoveWindowRule(ShortcutWindowRuleEditor? rule)
    {
        if (rule is not null)
        {
            SelectedTask?.RemoveWindowRule(rule);
        }
    }

    [RelayCommand]
    private async Task RefreshWindowRuleValuesAsync(ShortcutWindowRuleEditor? rule)
    {
        if (rule is null || _windowManager is null || rule.IsRefreshingWindows)
        {
            return;
        }

        rule.IsRefreshingWindows = true;
        try
        {
            var windows = await _windowManager.GetWindowsAsync(CancellationToken.None).ConfigureAwait(true);
            IEnumerable<string> values = rule.Field switch
            {
                TriggerField.WindowClass => windows.Select(window => window.Class),
                TriggerField.WindowTitle => windows.Select(window => window.Title),
                TriggerField.ProcessName => windows.Select(window => window.ProcessName),
                TriggerField.Workspace or TriggerField.None => [],
                _ => [],
            };

            var distinct = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.OrdinalIgnoreCase);

            rule.AvailableWindowValues.Clear();
            foreach (var value in distinct)
            {
                rule.AvailableWindowValues.Add(value);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[ShortcutViewModel] Failed to fetch window list");
        }
        finally
        {
            rule.IsRefreshingWindows = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await SaveChangesAsync(showSuccessStatus: true).ConfigureAwait(false);
    }

    private Task SaveChangesAsync(bool showSuccessStatus)
    {
        var selected = SelectedTask;
        var draft = selected?.ToCore();
        var scope = selected?.ScopeGeneration ?? _projection.ScopeGeneration;
        return draft is null ? Task.CompletedTask : PersistMutationAsync(
            async () => { _ = await _manageShortcut.UpdateAsync(draft, scope, CancellationToken.None).ConfigureAwait(false); }, showSuccessStatus);
    }

    private async Task PersistMutationAsync(Func<Task> mutation, bool showSuccessStatus = false, Guid? selectTaskId = null)
    {
        if (_disposed) { return; }
        var selectedTaskId = selectTaskId ?? SelectedTask?.Id;
        try
        {
            await _projection.ApplyMutationAsync(mutation, () => RefreshEditorsAsync(), selectedTaskId, () =>
            {
                OnPropertyChanged(nameof(TaskCountText));
                if (showSuccessStatus) { RaiseStatus(LocalizationService["Shortcut_StatusChangesSaved"]); }
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (_disposed) { return; }
            if (ex is TaskScopeConflictException) { await RefreshEditorsAsync().ConfigureAwait(false); }
            Log.LogError(ex, "[ShortcutViewModel] Failed to save shortcut tasks");
            var status = string.Format(LocalizationService.CurrentCulture, LocalizationService["Shortcut_StatusSaveFailed"], ex.Message);
            await RunOnUiThreadAsync(() =>
            {
                if (_disposed) { return; }
                RaiseStatus(status);
            }).ConfigureAwait(false);
            try
            {
                if (_disposed) { return; }
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
        await PersistMutationAsync(async () => { _ = await _manageShortcut.SetEnabledAsync(new TaskRequest(task.Id, task.IsEnabled, task.ScopeGeneration), CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
    }

    private void OnShortcutStarting(object? sender, ShortcutStartingEventArgs e)
    {
        var task = e.Task;
        UiDispatcher.Post(() =>
        {
            if (_disposed || !_shortcutService.IsCurrentTask(e.Task)) { return; }
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
        UiDispatcher.Post(() =>
        {
            if (_disposed || !_shortcutService.IsCurrentTask(e.Task)) { return; }
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

    private void RemapEditors() => _ = RefreshEditorsAsync();

    internal async Task RefreshEditorsAsync(bool propagateError = false)
    {
        try { await _projection.RefreshAsync().ConfigureAwait(false); }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (propagateError) { throw; }
            Log.LogError(error, "[ShortcutViewModel] Failed to refresh task projection");
        }
    }

    private void SyncRuntimeStatus(ShortcutTask task)
    {
        if (_projection.TryGetEditor(task.Id, out var editor))
        {
            editor.SyncRuntimeStatus(task.LastTriggeredTime, task.LastStatus);
        }
    }

    private void RaiseStatus(string message)
    {
        if (UiDispatcher.CheckAccess())
        {
            StatusChanged?.Invoke(this, message);
            return;
        }

        UiDispatcher.Post(() => StatusChanged?.Invoke(this, message));
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
        _projection.Dispose();

        _shortcutService.ShortcutStarting -= OnShortcutStarting;
        _shortcutService.ShortcutExecuted -= OnShortcutExecuted;
        _shortcutService.Tasks?.CollectionChanged -= OnTasksCollectionChanged;
        SelectedTask?.PropertyChanged -= OnSelectedTaskPropertyChanged;
        LocalizationService.CultureChanged -= OnCultureChanged;
    }
}
