using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using CrossMacro.Application.Automation;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Triggers tab - manages window-match triggers that switch profiles.
/// </summary>
public partial class TriggerViewModel : ViewModelBase, IDisposable
{
    private readonly ITriggerService _triggerService;
    private readonly IProfileManager? _profileManager;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IWindowManager? _windowManager;
    private readonly IManageTrigger? _manageTrigger;
    private TriggerTask? _selectedTask;
    private IReadOnlyList<ProfileInfo> _availableProfiles = [];
    private bool _isRefreshingWindows;
    private bool _disposed;

    public ObservableCollection<TriggerTask> Tasks => _triggerService.Tasks;

    public ILocalizationService LocalizationService => _localizationService;

    public Task InitializationTask { get; }

    /// <summary>
    /// Values extracted from running windows for the current Field (Class/Title/Workspace).
    /// Populated on demand via RefreshWindowsCommand. Empty until first refresh.
    /// </summary>
    public ObservableCollection<string> AvailableWindowValues { get; } = [];

    public bool IsRefreshingWindows
    {
        get => _isRefreshingWindows;
        private set
        {
            if (SetProperty(ref _isRefreshingWindows, value))
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
            if (SelectedTask != null && !string.IsNullOrEmpty(value))
            {
                SelectedTask.Value = value;
            }
            // Always notify so the ComboBox resets to placeholder (no persistent selection).
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<ProfileInfo> AvailableProfiles
    {
        get => _availableProfiles;
        private set => SetProperty(ref _availableProfiles, value);
    }

    public string TaskCountText => string.Format(
        _localizationService.CurrentCulture,
        _localizationService["Trigger_ItemsText"],
        Tasks.Count);

    public bool IsMonitoring => _triggerService.IsMonitoring;

    public TriggerTask? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (_selectedTask != value)
            {
                if (_selectedTask != null)
                {
                    _selectedTask.PropertyChanged -= OnSelectedTaskPropertyChanged;
                }

                _selectedTask = value;
                if (_selectedTask != null)
                {
                    _selectedTask.PropertyChanged += OnSelectedTaskPropertyChanged;
                }
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
        if (e.PropertyName is nameof(TriggerTask.LastTriggeredTime) or nameof(TriggerTask.LastStatus))
        {
            RaiseOnUiThread(OnSelectedTaskStatusChanged);
        }

        // When the field changes, the previously fetched window values no longer apply.
        if (e.PropertyName == nameof(TriggerTask.Field))
        {
            RaiseOnUiThread(() =>
            {
                AvailableWindowValues.Clear();
                RefreshWindowsCommand.NotifyCanExecuteChanged();
            });
        }
    }

    public bool HasSelectedTask => SelectedTask != null;

    /// <summary>
    /// Bridge between the XAML ComboBox (binds <see cref="ProfileInfo"/> objects)
    /// and the persisted <see cref="TriggerTask.TargetProfileId"/> string.
    /// </summary>
    public ProfileInfo? TargetProfileInfo
    {
        get
        {
            if (SelectedTask == null || string.IsNullOrEmpty(SelectedTask.TargetProfileId) || _profileManager == null)
                return null;
            return AvailableProfiles.FirstOrDefault(p => p.Id == SelectedTask.TargetProfileId);
        }
        set
        {
            if (SelectedTask != null)
            {
                var newId = value?.Id ?? string.Empty;
                if (SelectedTask.TargetProfileId != newId)
                {
                    SelectedTask.TargetProfileId = newId;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedTask));
                }
            }
        }
    }

    public string SelectedLastTriggeredText =>
        SelectedTask?.LastTriggeredTime?.ToLocalTime().ToString("G", _localizationService.CurrentCulture)
        ?? _localizationService["Trigger_Never"];

    public string SelectedStatusText => string.IsNullOrWhiteSpace(SelectedTask?.LastStatus)
        ? _localizationService["Trigger_StatusPlaceholder"]
        : SelectedTask.LastStatus!;

    public event EventHandler<string>? StatusChanged;

    public TriggerViewModel(
        ITriggerService triggerService,
        IProfileManager? profileManager,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IWindowManager? windowManager)
    {
        _triggerService = triggerService;
        _profileManager = profileManager;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _windowManager = windowManager;
        _localizationService.CultureChanged += OnCultureChanged;

        _triggerService.TriggerFired += OnTriggerFired;
        _triggerService.Tasks.CollectionChanged += OnTasksCollectionChanged;

        if (_profileManager != null)
        {
            _profileManager.ProfileChanged += OnProfileChanged;
        }

        InitializationTask = InitializeAsyncSafe();
    }

    public TriggerViewModel(
        IManageTrigger manageTrigger,
        ITriggerService triggerService,
        IProfileManager? profileManager,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IWindowManager? windowManager)
        : this(triggerService, profileManager, dialogService, localizationService, windowManager)
    {
        _manageTrigger = manageTrigger;
    }

    private async Task InitializeAsyncSafe()
    {
        try
        {
            await _triggerService.LoadAsync();
            RefreshProfileData();
            _triggerService.Start();
            OnPropertyChanged(nameof(IsMonitoring));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TriggerViewModel] Failed to initialize triggers");
            RaiseStatus(string.Format(
                _localizationService.CurrentCulture,
                _localizationService["Trigger_StatusInitFailed"],
                ex.Message));
        }
    }

    public void RefreshProfileData()
    {
        if (_profileManager != null)
        {
            AvailableProfiles = _profileManager.Profiles.ToArray();
        }
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
                _localizationService.CurrentCulture,
                _localizationService["Trigger_DefaultTaskName"],
                Tasks.Count + 1)
        };
        if (_manageTrigger is not null)
        {
            await _manageTrigger.AddAsync(task);
        }
        else
        {
            _triggerService.AddTask(task);
        }
        SelectedTask = task;
        OnPropertyChanged(nameof(TaskCountText));
    }

    [RelayCommand]
    private async Task RemoveTaskAsync(TriggerTask? task)
    {
        if (task == null) return;

        var confirmed = await _dialogService.ShowConfirmationAsync(
            _localizationService["Trigger_DeleteTitle"],
            string.Format(
                _localizationService.CurrentCulture,
                _localizationService["Trigger_DeleteMessage"],
                task.Name));

        if (!confirmed) return;

        if (_manageTrigger is not null)
        {
            var selectedTaskId = SelectedTask?.Id;
            await _manageTrigger.RemoveAsync(new TaskRequest(task.Id));
            SelectedTask = selectedTaskId is Guid id
                ? Tasks.FirstOrDefault(candidate => candidate.Id == id) ?? Tasks.FirstOrDefault()
                : Tasks.FirstOrDefault();
            OnPropertyChanged(nameof(TaskCountText));
            return;
        }

        var wasSelected = SelectedTask?.Id == task.Id;
        _triggerService.RemoveTask(task.Id);
        if (wasSelected)
        {
            SelectedTask = Tasks.FirstOrDefault();
        }
        await SaveChangesAsync(showSuccessStatus: false, rollback: () =>
        {
            _triggerService.AddTask(task);
            if (wasSelected)
            {
                SelectedTask = task;
            }
        });
    }

    [RelayCommand]
    private void SelectTask(TriggerTask? task)
    {
        if (task != null)
        {
            SelectedTask = SelectedTask?.Id == task.Id ? null : task;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await SaveChangesAsync(showSuccessStatus: true);
    }

    [RelayCommand]
    private async Task BrowseMacroAsync()
    {
        if (SelectedTask == null) return;

        var filters = new FileDialogFilter[]
        {
            new FileDialogFilter { Name = _localizationService["Trigger_OpenMacroDialogFilter"], Extensions = new[] { "macro" } }
        };

        var filePath = await _dialogService.ShowOpenFileDialogAsync(
            _localizationService["Trigger_OpenMacroDialogTitle"],
            filters);

        if (!string.IsNullOrEmpty(filePath) && SelectedTask != null)
        {
            SelectedTask.MacroFilePath = filePath;
            OnPropertyChanged(nameof(SelectedTask));
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefreshWindows))]
    private async Task RefreshWindowsAsync()
    {
        if (SelectedTask == null || _windowManager == null) return;

        IsRefreshingWindows = true;
        try
        {
            var windows = await _windowManager.GetWindowsAsync(CancellationToken.None)
                .ConfigureAwait(true); // stay on UI thread after await

            var field = SelectedTask.Field;
            IEnumerable<string> values = field switch
            {
                TriggerField.WindowClass  => windows.Select(w => w.Class),
                TriggerField.WindowTitle  => windows.Select(w => w.Title),
                TriggerField.Workspace    => windows.Select(w => w.Workspace),
                TriggerField.ProcessName  => windows.Select(w => w.ProcessName),
                _                         => []
            };

            var distinct = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AvailableWindowValues.Clear();
            foreach (var v in distinct)
                AvailableWindowValues.Add(v);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TriggerViewModel] Failed to fetch window list");
        }
        finally
        {
            IsRefreshingWindows = false;
        }
    }

    private bool CanRefreshWindows() => !IsRefreshingWindows && SelectedTask?.Field != TriggerField.None;

    private async Task SaveChangesAsync(bool showSuccessStatus, Action? rollback = null)
    {
        try
        {
            if (_manageTrigger is not null && SelectedTask is not null)
            {
                await _manageTrigger.UpdateAsync(SelectedTask);
            }
            else
            {
                await _triggerService.SaveAsync();
            }
            if (showSuccessStatus)
            {
                RaiseStatus(_localizationService["Trigger_StatusChangesSaved"]);
            }
        }
        catch (Exception ex)
        {
            rollback?.Invoke();
            Log.Error(ex, "[TriggerViewModel] Failed to save trigger tasks");
            var status = string.Format(
                _localizationService.CurrentCulture,
                _localizationService["Trigger_StatusSaveFailed"],
                ex.Message);
            RaiseStatus(status);
            try
            {
                await _dialogService.ShowMessageAsync(_localizationService["Trigger_SaveFailedTitle"], status);
            }
            catch (Exception dialogEx)
            {
                Log.Warning(dialogEx, "[TriggerViewModel] Failed to show save error dialog");
            }
        }
    }

    [RelayCommand]
    private async Task TaskEnabledChangedAsync(TriggerTask task)
    {
        var previousEnabled = !task.IsEnabled;
        if (_manageTrigger is not null)
        {
            var selectedTaskId = SelectedTask?.Id;
            try
            {
                await _manageTrigger.SetEnabledAsync(new TaskRequest(task.Id, task.IsEnabled));
            }
            finally
            {
                SelectedTask = selectedTaskId is Guid id
                    ? Tasks.FirstOrDefault(candidate => candidate.Id == id)
                    : null;
            }
            return;
        }

        if (_manageTrigger is null)
        {
            _triggerService.SetTaskEnabled(task.Id, task.IsEnabled);
        }
        await SaveChangesAsync(showSuccessStatus: false, rollback: () => _triggerService.SetTaskEnabled(task.Id, previousEnabled));
    }

    private void OnTriggerFired(object? sender, TriggerFiredEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var statusText = e.Success
                ? string.Format(
                    _localizationService.CurrentCulture,
                    _localizationService["Trigger_StatusFired"],
                    e.Task.Name,
                    e.Message ?? "")
                : string.Format(
                    _localizationService.CurrentCulture,
                    _localizationService["Trigger_StatusFailed"],
                    e.Task.Name,
                    e.Message ?? "");
            RaiseStatus(statusText);

            if (SelectedTask?.Id == e.Task.Id)
            {
                OnPropertyChanged(nameof(SelectedTask));
                OnSelectedTaskStatusChanged();
            }
        });
    }

    private void OnProfileChanged(object? sender, ProfileInfo profile)
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
        OnPropertyChanged(nameof(TaskCountText));
    }

    private static void RaiseOnUiThread(Action action)
    {
        if (Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    private void RaiseStatus(string message)
    {
        if (Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            StatusChanged?.Invoke(this, message);
            return;
        }

        Dispatcher.UIThread.Post(() => StatusChanged?.Invoke(this, message));
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TaskCountText));
        OnPropertyChanged(nameof(SelectedTask));
        OnSelectedTaskStatusChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _triggerService.TriggerFired -= OnTriggerFired;
        _triggerService.Tasks.CollectionChanged -= OnTasksCollectionChanged;
        if (_selectedTask != null)
        {
            _selectedTask.PropertyChanged -= OnSelectedTaskPropertyChanged;
        }
        if (_profileManager != null)
        {
            _profileManager.ProfileChanged -= OnProfileChanged;
        }
        _localizationService.CultureChanged -= OnCultureChanged;
    }
}
