
namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Triggers tab - manages window-match triggers that switch profiles.
/// </summary>
public partial class TriggerViewModel : ViewModelBase, IDisposable
{
    private readonly ITriggerService _triggerService;
    private readonly IProfileManager? _profileManager;
    private readonly IProfileRuntimeState? _profileRuntimeState;
    private readonly IDialogService _dialogService;
    private readonly IWindowManager? _windowManager;
    private readonly IManageTrigger? _manageTrigger;
    private readonly Dictionary<Guid, TriggerTaskEditor> _editors = [];
    private bool _disposed;

    public ObservableCollection<TriggerTaskEditor> Tasks { get; } = [];

    public ILocalizationService LocalizationService { get; }

    public Task InitializationTask { get; }

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
        ITriggerService triggerService,
        IProfileManager? profileManager,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IWindowManager? windowManager,
        IProfileRuntimeState? profileRuntimeState = null)
    {
        _triggerService = triggerService;
        _profileManager = profileManager;
        _dialogService = dialogService;
        LocalizationService = localizationService;
        _windowManager = windowManager;
        _profileRuntimeState = profileRuntimeState;
        LocalizationService.CultureChanged += OnCultureChanged;

        _triggerService.TriggerFired += OnTriggerFired;
        _triggerService.Tasks.CollectionChanged += OnTasksCollectionChanged;

        _profileManager?.ProfileChanged += OnProfileChanged;

        InitializationTask = InitializeAsyncSafeAsync();
    }

    public TriggerViewModel(
        IManageTrigger manageTrigger,
        ITriggerService triggerService,
        IProfileManager? profileManager,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IWindowManager? windowManager,
        IProfileRuntimeState? profileRuntimeState = null)
        : this(triggerService, profileManager, dialogService, localizationService, windowManager, profileRuntimeState)
    {
        _manageTrigger = manageTrigger;
    }

    private async Task InitializeAsyncSafeAsync()
    {
        try
        {
            // ProfileRuntimeCoordinator owns the initial profile load before the shell is composed.
            if (_profileRuntimeState?.IsInitialized is not true)
            {
                await _triggerService.LoadAsync().ConfigureAwait(false);
            }

            _triggerService.Start();
            await RunOnUiThreadAsync(() =>
            {
                RefreshProfileData();
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
        var task = new TriggerTask
        {
            Name = string.Format(
                LocalizationService.CurrentCulture,
                LocalizationService["Trigger_DefaultTaskName"],
                Tasks.Count + 1),
        };
        if (_manageTrigger is not null)
        {
            _ = await _manageTrigger.AddAsync(task, default).ConfigureAwait(false);
        }
        else
        {
            _triggerService.AddTask(task);
        }
        await RunOnUiThreadAsync(() =>
        {
            RemapEditors();
            SelectedTask = _editors[task.Id];
            OnPropertyChanged(nameof(TaskCountText));
        }).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RemoveTaskAsync(TriggerTaskEditor? task)
    {
        if (task is null)
        {
            return;
        }

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

        if (_manageTrigger is not null)
        {
            var selectedTaskId = SelectedTask?.Id;
            _ = await _manageTrigger.RemoveAsync(new TaskRequest(task.Id), default).ConfigureAwait(false);
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
            _triggerService.RemoveTask(task.Id);
            if (wasSelected)
            {
                SelectedTask = Tasks.FirstOrDefault();
            }
        }).ConfigureAwait(false);
        await SaveChangesAsync(showSuccessStatus: false, rollback: () =>
        {
            _triggerService.AddTask(task.ToCore());
            if (wasSelected)
            {
                SelectedTask = task;
            }
        }).ConfigureAwait(false);
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

    private async Task SaveChangesAsync(bool showSuccessStatus, Action? rollback = null)
    {
        try
        {
            if (_manageTrigger is not null && SelectedTask is not null)
            {
                _ = await _manageTrigger.UpdateAsync(SelectedTask.ToCore(), default).ConfigureAwait(false);
            }
            else if (_manageTrigger is null && SelectedTask is { } selectedTask)
            {
                await RunOnUiThreadAsync(() =>
                {
                    selectedTask.ApplyToCore(_triggerService.Tasks.First(task => task.Id == selectedTask.Id));
                }).ConfigureAwait(false);
                await _triggerService.SaveAsync().ConfigureAwait(false);
            }
            if (showSuccessStatus)
            {
                await RunOnUiThreadAsync(() => RaiseStatus(LocalizationService["Trigger_StatusChangesSaved"])).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[TriggerViewModel] Failed to save trigger tasks");
            var status = string.Format(
                LocalizationService.CurrentCulture,
                LocalizationService["Trigger_StatusSaveFailed"],
                ex.Message);
            await RunOnUiThreadAsync(() =>
            {
                SelectedTask?.Rollback();
                rollback?.Invoke();
                RaiseStatus(status);
            }).ConfigureAwait(false);
            try
            {
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
        var previousEnabled = !task.IsEnabled;
        if (_manageTrigger is not null)
        {
            var selectedTaskId = SelectedTask?.Id;
            try
            {
                _ = await _manageTrigger.SetEnabledAsync(new TaskRequest(task.Id, task.IsEnabled), default).ConfigureAwait(false);
            }
            finally
            {
                await RunOnUiThreadAsync(() =>
                {
                    RemapEditors();
                    SelectedTask = selectedTaskId is Guid id
                        ? Tasks.FirstOrDefault(candidate => candidate.Id == id)
                        : null;
                }).ConfigureAwait(false);
            }
            return;
        }

        _triggerService.SetTaskEnabled(task.Id, task.IsEnabled);
        await SaveChangesAsync(showSuccessStatus: false, rollback: () => _triggerService.SetTaskEnabled(task.Id, previousEnabled)).ConfigureAwait(false);
    }

    private void OnTriggerFired(object? sender, TriggerFiredEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
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
                if (_editors.TryGetValue(e.Task.Id, out var editor))
                {
                    editor.SyncRuntimeStatus(e.Task.LastTriggeredTime, e.Task.LastStatus);
                }
                OnSelectedTaskStatusChanged();
            }
        });
    }

    private void OnProfileChanged(object? sender, ProfileChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(RefreshProfileData);
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
        var current = _triggerService.Tasks.ToDictionary(task => task.Id);
        foreach (var task in _triggerService.Tasks)
        {
            if (!_editors.TryGetValue(task.Id, out var editor))
            {
                editor = new TriggerTaskEditor();
                _editors[task.Id] = editor;
            }
            editor.Load(task);
            if (!Tasks.Contains(editor))
            {
                Tasks.Add(editor);
            }
        }
        foreach (var editor in Tasks.Where(editor => !current.ContainsKey(editor.Id)).ToArray())
        {
            _ = Tasks.Remove(editor);
            _ = _editors.Remove(editor.Id);
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

        _triggerService.TriggerFired -= OnTriggerFired;
        _triggerService.Tasks.CollectionChanged -= OnTasksCollectionChanged;
        SelectedTask?.PropertyChanged -= OnSelectedTaskPropertyChanged;
        _profileManager?.ProfileChanged -= OnProfileChanged;
        LocalizationService.CultureChanged -= OnCultureChanged;
    }
}
