
namespace CrossMacro.UI.ViewModels.Automation;

/// <summary>
/// ViewModel for the Triggers tab - manages window-match triggers that switch profiles.
/// </summary>
public partial class TriggerViewModel : AutomationTaskTabViewModel<TriggerTask, TriggerTaskEditor>
{
    private readonly ITriggerService _triggerService;
    private readonly IProfileManager? _profileManager;
    private readonly IWindowManager? _windowManager;

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

    public bool IsMonitoring => _triggerService.IsMonitoring;

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
        ?? Localize("Never");

    public string SelectedStatusText => string.IsNullOrWhiteSpace(SelectedTask?.LastStatus)
        ? Localize("StatusPlaceholder")
        : SelectedTask.LastStatus;

    public TriggerViewModel(
        IManageAutomationTasks<TriggerTask> manageTrigger,
        ITriggerService triggerService,
        IProfileManager? profileManager,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IWindowManager? windowManager,
        IUiDispatcher? uiDispatcher = null)
        : base(manageTrigger, TriggerTaskKind.Instance, localizationService, dialogService, "Trigger", uiDispatcher)
    {
        _triggerService = triggerService ?? throw new ArgumentNullException(nameof(triggerService));
        _profileManager = profileManager;
        _windowManager = windowManager;

        _triggerService.TriggerFired += OnTriggerFired;
        _triggerService.Tasks.CollectionChanged += OnServiceTasksChanged;
        _profileManager?.ProfileChanged += OnProfileChanged;
    }

    private protected override TriggerTask CreateTask(string name) => new() { Name = name };

    private protected override void OnInitialized()
    {
        RefreshProfilesAndMonitoringState();
    }

    private protected override void OnProfileDataRefreshing()
    {
        RefreshProfilesAndMonitoringState();
    }

    private void RefreshProfilesAndMonitoringState()
    {
        if (_profileManager is not null)
        {
            AvailableProfiles = _profileManager.Profiles.ToArray();
        }

        OnPropertyChanged(nameof(IsMonitoring));
    }

    private protected override void OnSelectedTaskChanged()
    {
        AvailableWindowValues.Clear();
        OnSelectedTaskStatusChanged();
        RefreshWindowsCommand.NotifyCanExecuteChanged();
    }

    private protected override void OnSelectedTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
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

    private protected override void OnCultureChangedOnUiThread()
    {
        OnSelectedTaskStatusChanged();
        base.OnCultureChangedOnUiThread();
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
    private Task BrowseMacroAsync() =>
        PickMacroFileAsync("OpenMacroDialogTitle", static (task, path) => task.MacroFilePath = path);

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
            var values = await WindowFieldValues.ListAsync(_windowManager, SelectedTask.Field).ConfigureAwait(true);
            AvailableWindowValues.Clear();
            foreach (var value in values)
            {
                AvailableWindowValues.Add(value);
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

    private void OnTriggerFired(object? sender, TriggerFiredEventArgs e)
    {
        UiDispatcher.Post(() =>
        {
            if (IsDisposed || !_triggerService.IsCurrentTask(e.Task)) { return; }
            ReportStatus(FormatLocalized(e.Success ? "StatusFired" : "StatusFailed", e.Task.Name, e.Message ?? ""));

            if (SelectedTask?.Id == e.Task.Id)
            {
                if (TryGetEditor(e.Task.Id, out var editor))
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

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            _triggerService.TriggerFired -= OnTriggerFired;
            _triggerService.Tasks.CollectionChanged -= OnServiceTasksChanged;
            _profileManager?.ProfileChanged -= OnProfileChanged;
        }

        base.Dispose(disposing);
    }
}
