namespace CrossMacro.UI.ViewModels.Automation;

/// <summary>
/// Shared behavior of the Schedule, Shortcut and Trigger tabs: a list of persisted tasks projected
/// into detached editors, with add, remove, save and enable commands that go through the Application
/// use cases and then refresh the projection.
/// </summary>
public abstract partial class AutomationTaskTabViewModel<TTask, TEditor> : AutomationTabViewModelBase
    where TTask : class
    where TEditor : TaskEditorBase<TTask>, new()
{
    private readonly IDialogService _dialogService;
    private readonly ScopedTaskProjection<TTask, TEditor> _projection;

    protected AutomationTaskTabViewModel(
        ILocalizationService localizationService,
        IDialogService dialogService,
        string resourcePrefix,
        IUiDispatcher? uiDispatcher)
        : base(localizationService, resourcePrefix, uiDispatcher)
    {
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _projection = new ScopedTaskProjection<TTask, TEditor>(
            ListTasksAsync, UiDispatcher, GetTaskId,
            static scope => new TEditor { ScopeGeneration = scope },
            () => SelectedTask, selected => SelectedTask = selected,
            () => { OnPropertyChanged(nameof(Tasks)); OnPropertyChanged(nameof(TaskCountText)); });
        LocalizationService.CultureChanged += OnCultureChanged;
    }

    public ObservableCollection<TEditor> Tasks => _projection.Items;

    public string TaskCountText => FormatLocalized("ItemsText", Tasks.Count);

    public bool HasSelectedTask => SelectedTask is not null;

    public TEditor? SelectedTask
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field?.PropertyChanged -= OnSelectedTaskPropertyChanged;
            field = value;
            field?.PropertyChanged += OnSelectedTaskPropertyChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedTask));
            OnSelectedTaskChanged();
        }
    }

    protected long ScopeGeneration => _projection.ScopeGeneration;

    private protected abstract Task<TaskCollectionResult<TTask>> ListTasksAsync(CancellationToken cancellationToken);

    private protected abstract Guid GetTaskId(TTask task);

    private protected abstract TTask CreateTask(string name);

    private protected abstract Task AddToStoreAsync(TTask task, long scopeGeneration, CancellationToken cancellationToken);

    private protected abstract Task UpdateInStoreAsync(TTask task, long scopeGeneration, CancellationToken cancellationToken);

    private protected abstract Task RemoveFromStoreAsync(Guid id, long scopeGeneration, CancellationToken cancellationToken);

    /// <summary>Raised after <see cref="SelectedTask"/> changed and its own notifications were sent.</summary>
    private protected virtual void OnSelectedTaskChanged() { /* Empty */ }

    private protected virtual void OnSelectedTaskPropertyChanged(object? sender, PropertyChangedEventArgs e) { /* Empty */ }

    private protected virtual void OnTaskEnabledChanged(TEditor task) { /* Empty */ }

    /// <summary>Called on the UI thread after the first load, before the first task is selected.</summary>
    private protected virtual void OnInitialized() { /* Empty */ }

    /// <summary>Called before the projection is refreshed for a profile change.</summary>
    private protected virtual void OnProfileDataRefreshing() { /* Empty */ }

    private protected virtual void OnCultureChangedOnUiThread()
    {
        OnPropertyChanged(nameof(TaskCountText));
        OnPropertyChanged(nameof(SelectedTask));
    }

    protected override async Task InitializeCoreAsync()
    {
        await RefreshEditorsAsync(propagateError: true).ConfigureAwait(false);
        await RunOnUiThreadAsync(() =>
        {
            if (IsDisposed)
            {
                return;
            }

            OnInitialized();
            SelectedTask = Tasks.FirstOrDefault();
        }).ConfigureAwait(false);
    }

    public void RefreshProfileData()
    {
        if (IsDisposed)
        {
            return;
        }

        OnProfileDataRefreshing();
        _ = RefreshEditorsAsync();
    }

    internal async Task RefreshEditorsAsync(bool propagateError = false, Guid? discardDraftOf = null)
    {
        try
        {
            await _projection.RefreshAsync(discardDraftOf).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (propagateError)
            {
                throw;
            }

            Log.LogError(error, "[{ViewModel}] Failed to refresh task projection", GetType().Name);
        }
    }

    /// <summary>Refreshes the projection when the service's task list changed.</summary>
    protected void OnServiceTasksChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        PostToUiThread(() => _ = RefreshEditorsAsync());

    protected bool TryGetEditor(Guid id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TEditor? editor) =>
        _projection.TryGetEditor(id, out editor);

    [RelayCommand]
    private async Task AddTaskAsync()
    {
        var scope = ScopeGeneration;
        var task = CreateTask(FormatLocalized("DefaultTaskName", Tasks.Count + 1));
        await PersistMutationAsync(
            () => AddToStoreAsync(task, scope, CancellationToken.None),
            selectTaskId: GetTaskId(task)).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RemoveTaskAsync(TEditor? task)
    {
        if (task is null)
        {
            return;
        }

        var scope = task.ScopeGeneration;
        var id = task.Id;
        var confirmed = await _dialogService.ShowConfirmationAsync(
            Localize("DeleteTitle"),
            FormatLocalized("DeleteMessage", task.Name)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        await PersistMutationAsync(() => RemoveFromStoreAsync(id, scope, CancellationToken.None)).ConfigureAwait(false);
    }

    [RelayCommand]
    private Task SaveAsync() => SelectedTask is { } selected
        ? PersistDraftAsync(selected, showSuccessStatus: true, discardDraftOnFailure: false)
        : Task.CompletedTask;

    [RelayCommand]
    private Task TaskEnabledChangedAsync(TEditor task)
    {
        OnTaskEnabledChanged(task);
        return PersistDraftAsync(task, showSuccessStatus: false, discardDraftOnFailure: true);
    }

    /// <summary>Lets the user pick a macro file and applies it to the selected task if that task is still current.</summary>
    protected async Task PickMacroFileAsync(string dialogTitleKeySuffix, Action<TEditor, string> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        var selectedTask = SelectedTask;
        if (selectedTask is null)
        {
            return;
        }

        var scope = selectedTask.ScopeGeneration;
        var filePath = await _dialogService.ShowOpenFileDialogAsync(
            Localize(dialogTitleKeySuffix),
            MacroFileFormat.CreateDialogFilters(Localize("OpenMacroDialogFilter"))).ConfigureAwait(false);

        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            if (IsDisposed || scope != ScopeGeneration || !ReferenceEquals(SelectedTask, selectedTask))
            {
                ReportStatus(LocalizationService["Automation_StatusFilePickerStale"]);
                return;
            }

            apply(selectedTask, filePath);
            OnPropertyChanged(nameof(SelectedTask));
        }).ConfigureAwait(false);
    }

    private Task PersistDraftAsync(TEditor editor, bool showSuccessStatus, bool discardDraftOnFailure)
    {
        var draft = editor.ToCore();
        var scope = editor.ScopeGeneration;
        return PersistMutationAsync(
            () => UpdateInStoreAsync(draft, scope, CancellationToken.None),
            showSuccessStatus,
            persistedDraftId: editor.Id,
            discardDraftOnFailure: discardDraftOnFailure);
    }

    private async Task PersistMutationAsync(
        Func<Task> mutation,
        bool showSuccessStatus = false,
        Guid? selectTaskId = null,
        Guid? persistedDraftId = null,
        bool discardDraftOnFailure = false)
    {
        if (IsDisposed)
        {
            return;
        }

        var selectedTaskId = selectTaskId ?? SelectedTask?.Id;
        try
        {
            await _projection.ApplyMutationAsync(
                mutation,
                () => RefreshEditorsAsync(discardDraftOf: persistedDraftId),
                selectedTaskId,
                () =>
                {
                    OnPropertyChanged(nameof(TaskCountText));
                    if (showSuccessStatus)
                    {
                        ReportStatus(Localize("StatusChangesSaved"));
                    }
                }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (IsDisposed)
            {
                return;
            }

            Log.LogError(ex, "[{ViewModel}] Failed to save tasks", GetType().Name);
            await RefreshEditorsAsync(discardDraftOf: discardDraftOnFailure ? persistedDraftId : null).ConfigureAwait(false);
            var status = FormatLocalized("StatusSaveFailed", ex.Message);
            ReportStatus(status);
            await ShowSaveFailureAsync(status).ConfigureAwait(false);
        }
    }

    private async Task ShowSaveFailureAsync(string status)
    {
        try
        {
            if (!IsDisposed)
            {
                await _dialogService.ShowMessageAsync(Localize("SaveFailedTitle"), status).ConfigureAwait(false);
            }
        }
        catch (Exception dialogEx) when (dialogEx is not OutOfMemoryException)
        {
            Log.Warning(dialogEx, "[{ViewModel}] Failed to show save error dialog", GetType().Name);
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e) => PostToUiThread(OnCultureChangedOnUiThread);

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            _projection.Dispose();
            SelectedTask?.PropertyChanged -= OnSelectedTaskPropertyChanged;
            LocalizationService.CultureChanged -= OnCultureChanged;
        }

        base.Dispose(disposing);
    }
}
