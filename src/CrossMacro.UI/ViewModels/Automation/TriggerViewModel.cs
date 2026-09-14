
namespace CrossMacro.UI.ViewModels.Automation;

/// <summary>
/// ViewModel for the Triggers tab - manages window-match triggers that switch profiles.
/// </summary>
public partial class TriggerViewModel : ViewModelBase, IDisposable
{
    private readonly ITriggerService _triggerService;
    private readonly IProfileManager? _profileManager;
    private readonly IDialogService _dialogService;
    private readonly IWindowManager? _windowManager;
    private readonly IManageTrigger _manageTrigger;
    private readonly ScopedTaskProjection<TriggerTask, TriggerTaskEditor> _projection;
    private bool _disposed;

    public ObservableCollection<TriggerTaskEditor> Tasks => _projection.Items;

    public ILocalizationService LocalizationService { get; }

    public Task InitializationTask { get; private set; } = Task.CompletedTask;
    private readonly Lock _initializationGate = new();
    private bool _initializationStarted;

    /// <summary>
    /// Values extracted from running windows for the current Field (Class/Title/Workspace).
    /// Populated on demand via RefreshWindowsCommand. Empty until first refresh.
    /// </summary>
    public ObservableCollection<string> AvailableWindowValues { get; } = [];

    public bool IsRefreshingWindows
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                RefreshWindowsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Bridges the window picker ComboBox selection to SelectedTask.Value.
    /// Write-only from the binding perspective: setting it pushes the value into the task.
    /// Returns null always so the ComboBox shows the placeholder after selection.
    /// </summary>
    public string? SelectedWindowValue
    {
        get => null;
        set
        {
            if (SelectedTask is not null && !string.IsNullOrEmpty(value))
            {
                SelectedTask.Value = value;
            }
            // Always notify so the ComboBox resets to placeholder (no persistent selection).
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<ProfileInfo> AvailableProfiles
    {
        get;
        private set => SetProperty(ref field, value);
    } = [];

    public string TaskCountText => string.Format(
        LocalizationService.CurrentCulture,
        LocalizationService["Trigger_ItemsText"],
        Tasks.Count);

    public bool IsMonitoring => _triggerService.IsMonitoring;

    public TriggerTaskEditor? SelectedTask
    {
        get; set
        {
            if (field != value)
            {
                field?.PropertyChanged -= OnSelectedTaskPropertyChanged;

                field = value;
                field?.PropertyChanged += OnSelectedTaskPropertyChanged;
                AvailableWindowValues.Clear();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedTask));
                OnPropertyChanged(nameof(SelectedLastTriggeredText));
                OnPropertyChanged(nameof(SelectedStatusText));
                OnSelectedTaskStatusChanged();
                RefreshWindowsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private void OnSelectedTaskPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TriggerTaskEditor.LastTriggeredTime) or nameof(TriggerTaskEditor.LastStatus))
        {
            PostToUiThread(OnSelectedTaskStatusChanged);
        }

        // When the field changes, the previously fetched window values no longer apply.
        if (string.Equals(e.PropertyName, nameof(TriggerTaskEditor.Field), StringComparison.Ordinal))
        {
            PostToUiThread(() =>
            {
                AvailableWindowValues.Clear();
                RefreshWindowsCommand.NotifyCanExecuteChanged();
            });
        }
    }

    public bool HasSelectedTask => SelectedTask is not null;

    /// <summary>
    /// Bridge between the XAML ComboBox (binds <see cref="ProfileInfo"/> objects)
    /// and the persisted <see cref="TriggerTask.TargetProfileId"/> string.
    /// </summary>
    public ProfileInfo? TargetProfileInfo
    {
        get
        {
            if (SelectedTask is null || string.IsNullOrEmpty(SelectedTask.TargetProfileId) || _profileManager is null)
            {
                return null;
            }

            return AvailableProfiles.FirstOrDefault(p => string.Equals(p.Id, SelectedTask.TargetProfileId, StringComparison.Ordinal));
        }
        set
        {
            if (SelectedTask is not null)
            {
                var newId = value?.Id ?? string.Empty;
                if (!string.Equals(SelectedTask.TargetProfileId, newId, StringComparison.Ordinal))
                {
                    SelectedTask.TargetProfileId = newId;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedTask));
                }
            }
        }
    }

    public string SelectedLastTriggeredText =>
        SelectedTask?.LastTriggeredTime?.ToLocalTime().ToString("G", LocalizationService.CurrentCulture)
        ?? LocalizationService["Trigger_Never"];

    public string SelectedStatusText => string.IsNullOrWhiteSpace(SelectedTask?.LastStatus)
        ? LocalizationService["Trigger_StatusPlaceholder"]
        : SelectedTask.LastStatus;

    public event EventHandler<string>? StatusChanged;

    public TriggerViewModel(
        IManageTrigger manageTrigger,
        ITriggerService triggerService,
        IProfileManager? profileManager,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IWindowManager? windowManager,
        IProfileRuntimeState? profileRuntimeState = null,
        IUiDispatcher? uiDispatcher = null)
        : base(uiDispatcher)
    {
        _manageTrigger = manageTrigger ?? throw new ArgumentNullException(nameof(manageTrigger));
        _triggerService = triggerService ?? throw new ArgumentNullException(nameof(triggerService));
        _profileManager = profileManager;
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        LocalizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _windowManager = windowManager;
        _projection = new ScopedTaskProjection<TriggerTask, TriggerTaskEditor>(
            _manageTrigger.ListAsync, UiDispatcher, static task => task.Id,
            static scope => new TriggerTaskEditor { ScopeGeneration = scope },
            static (editor, task) => editor.Load(task),
            () => SelectedTask, selected => SelectedTask = selected,
            () => { OnPropertyChanged(nameof(Tasks)); OnPropertyChanged(nameof(TaskCountText)); });
        _ = profileRuntimeState; // Initialization now uses a scoped Application snapshot.
        LocalizationService.CultureChanged += OnCultureChanged;

        _triggerService.TriggerFired += OnTriggerFired;
        _triggerService.Tasks.CollectionChanged += OnTasksCollectionChanged;

        _profileManager?.ProfileChanged += OnProfileChanged;


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
            await RunOnUiThreadAsync(() =>
            {
                if (_disposed) { return; }
                if (_profileManager is not null) { AvailableProfiles = _profileManager.Profiles.ToArray(); }
                SelectedTask = Tasks.FirstOrDefault();
                OnPropertyChanged(nameof(IsMonitoring));
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[TriggerViewModel] Failed to initialize triggers");
            var status = string.Format(
                LocalizationService.CurrentCulture,
                LocalizationService["Trigger_StatusInitFailed"],
                ex.Message);
            await RunOnUiThreadAsync(() => RaiseStatus(status)).ConfigureAwait(false);
        }
    }

    public void RefreshProfileData()
    {
        if (_disposed) { return; }
        if (_profileManager is not null)
        {
            AvailableProfiles = _profileManager.Profiles.ToArray();
        }
        RemapEditors();
        SelectedTask = Tasks.FirstOrDefault();
        OnPropertyChanged(nameof(Tasks));
        OnPropertyChanged(nameof(TaskCountText));
        OnPropertyChanged(nameof(SelectedTask));
        OnPropertyChanged(nameof(IsMonitoring));
        OnSelectedTaskStatusChanged();
    }

    [RelayCommand]
    private async Task AddTaskAsync()
    {
        var scope = _projection.ScopeGeneration;
        var task = new TriggerTask
        {
            Name = string.Format(
                LocalizationService.CurrentCulture,
                LocalizationService["Trigger_DefaultTaskName"],
                Tasks.Count + 1),
        };
        await PersistMutationAsync(async () => { _ = await _manageTrigger.AddAsync(task, scope, CancellationToken.None).ConfigureAwait(false); }, selectTaskId: task.Id).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RemoveTaskAsync(TriggerTaskEditor? task)
    {
        if (task is null)
        {
            return;
        }

        var scope = task.ScopeGeneration;
        var confirmed = await _dialogService.ShowConfirmationAsync(
            LocalizationService["Trigger_DeleteTitle"],
            string.Format(
                LocalizationService.CurrentCulture,
                LocalizationService["Trigger_DeleteMessage"],
                task.Name)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        await PersistMutationAsync(async () => { _ = await _manageTrigger.RemoveAsync(new TaskRequest(task.Id, ExpectedScopeGeneration: scope), CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
    }

    [RelayCommand]
    private void SelectTask(TriggerTaskEditor? task)
    {
        if (task is not null)
        {
            SelectedTask = SelectedTask?.Id == task.Id ? null : task;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await SaveChangesAsync(showSuccessStatus: true).ConfigureAwait(false);
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
            new FileDialogFilter { Name = LocalizationService["Trigger_OpenMacroDialogFilter"], Extensions = ["macro"] },
        };

        var filePath = await _dialogService.ShowOpenFileDialogAsync(
            LocalizationService["Trigger_OpenMacroDialogTitle"],
            filters).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(filePath))
        {
            await RunOnUiThreadAsync(() =>
            {
                if (_disposed || scope != _projection.ScopeGeneration || !ReferenceEquals(SelectedTask, selectedTask))
                { RaiseStatus("The active profile or selected task changed. Reopen the file picker and try again."); return; }
                selectedTask.MacroFilePath = filePath;
                OnPropertyChanged(nameof(SelectedTask));
            }).ConfigureAwait(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefreshWindows))]
    private async Task RefreshWindowsAsync()
    {
        if (SelectedTask is null || _windowManager is null)
        {
            return;
        }

        IsRefreshingWindows = true;
        try
        {
            var windows = await _windowManager.GetWindowsAsync(CancellationToken.None)
                .ConfigureAwait(true); // stay on UI thread after await

            var field = SelectedTask.Field;
            IEnumerable<string> values = field switch
            {
                TriggerField.WindowClass => windows.Select(w => w.Class),
                TriggerField.WindowTitle => windows.Select(w => w.Title),
                TriggerField.Workspace => windows.Select(w => w.Workspace),
                TriggerField.ProcessName => windows.Select(w => w.ProcessName),
                TriggerField.None => [],
                _ => throw new InvalidOperationException("Unsupported trigger field."),
            };

            var distinct = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

            AvailableWindowValues.Clear();
            foreach (var v in distinct)
            {
                AvailableWindowValues.Add(v);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[TriggerViewModel] Failed to fetch window list");
        }
        finally
        {
            IsRefreshingWindows = false;
        }
    }

    private bool CanRefreshWindows() => !IsRefreshingWindows && (SelectedTask?.Field) is not TriggerField.None;

    private Task SaveChangesAsync(bool showSuccessStatus)
    {
        var selected = SelectedTask;
        var draft = selected?.ToCore();
        var scope = selected?.ScopeGeneration ?? _projection.ScopeGeneration;
        return draft is null ? Task.CompletedTask : PersistMutationAsync(
            async () => { _ = await _manageTrigger.UpdateAsync(draft, scope, CancellationToken.None).ConfigureAwait(false); }, showSuccessStatus);
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
                if (showSuccessStatus) { RaiseStatus(LocalizationService["Trigger_StatusChangesSaved"]); }
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (_disposed) { return; }
            if (ex is TaskScopeConflictException) { await RefreshEditorsAsync().ConfigureAwait(false); }
            Log.LogError(ex, "[TriggerViewModel] Failed to save trigger tasks");
            var status = string.Format(
                LocalizationService.CurrentCulture,
                LocalizationService["Trigger_StatusSaveFailed"],
                ex.Message);
            await RunOnUiThreadAsync(() =>
            {
                if (_disposed) { return; }
                RemapEditors();
                RaiseStatus(status);
            }).ConfigureAwait(false);
            try
            {
                if (_disposed) { return; }
                await _dialogService.ShowMessageAsync(LocalizationService["Trigger_SaveFailedTitle"], status).ConfigureAwait(false);
            }
            catch (Exception dialogEx) when (dialogEx is not OutOfMemoryException)
            {
                Log.Warning(dialogEx, "[TriggerViewModel] Failed to show save error dialog");
            }
        }
    }

    [RelayCommand]
    private async Task TaskEnabledChangedAsync(TriggerTaskEditor task)
    {
        await PersistMutationAsync(async () => { _ = await _manageTrigger.SetEnabledAsync(new TaskRequest(task.Id, task.IsEnabled, task.ScopeGeneration), CancellationToken.None).ConfigureAwait(false); }).ConfigureAwait(false);
    }

    private void OnTriggerFired(object? sender, TriggerFiredEventArgs e)
    {
        UiDispatcher.Post(() =>
        {
            if (_disposed || !_triggerService.IsCurrentTask(e.Task)) { return; }
            var statusText = e.Success
                ? string.Format(
                    LocalizationService.CurrentCulture,
                    LocalizationService["Trigger_StatusFired"],
                    e.Task.Name,
                    e.Message ?? "")
                : string.Format(
                    LocalizationService.CurrentCulture,
                    LocalizationService["Trigger_StatusFailed"],
                    e.Task.Name,
                    e.Message ?? "");
            RaiseStatus(statusText);

            if (SelectedTask?.Id == e.Task.Id)
            {
                if (_projection.TryGetEditor(e.Task.Id, out var editor))
                {
                    editor.SyncRuntimeStatus(e.Task.LastTriggeredTime, e.Task.LastStatus);
                }
                OnSelectedTaskStatusChanged();
            }
        });
    }

    private void OnProfileChanged(object? sender, ProfileChangedEventArgs e)
    {
        UiDispatcher.Post(RefreshProfileData);
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
            Log.LogError(error, "[TriggerViewModel] Failed to refresh task projection");
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

        _triggerService.TriggerFired -= OnTriggerFired;
        _triggerService.Tasks.CollectionChanged -= OnTasksCollectionChanged;
        SelectedTask?.PropertyChanged -= OnSelectedTaskPropertyChanged;
        _profileManager?.ProfileChanged -= OnProfileChanged;
        LocalizationService.CultureChanged -= OnCultureChanged;
    }
}
