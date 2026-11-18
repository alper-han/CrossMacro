
namespace CrossMacro.UI.ViewModels.Automation;

/// <summary>
/// ViewModel for the Shortcuts tab - manages shortcut-triggered macro tasks
/// </summary>
public partial class ShortcutViewModel : AutomationTaskTabViewModel<ShortcutTask, ShortcutTaskEditor>
{
    private readonly IShortcutService _shortcutService;
    private readonly IManageShortcut _manageShortcut;
    private readonly IShortcutHotkeyNormalizer _hotkeyNormalizer;
    private readonly IWindowManager? _windowManager;

    public IGlobalHotkeyService GlobalHotkeyService { get; }

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
            ? Localize("NoFileSelected")
            : Path.GetFileName(SelectedTask.MacroFilePath);

    public string SelectedMacroLabel => Localize(SelectedTask?.RunOnPressAndRelease is true ? "PressMacroFile" : "MacroFile");

    public string? SelectedReleaseMacroFilePath
    {
        get => string.IsNullOrEmpty(SelectedTask?.ReleaseMacroFilePath) ? null : SelectedTask.ReleaseMacroFilePath;
        set => SelectedTask?.ReleaseMacroFilePath = value ?? "";
    }

    public string SelectedReleaseMacroFileName =>
        string.IsNullOrEmpty(SelectedTask?.ReleaseMacroFilePath)
            ? Localize("NoFileSelected")
            : Path.GetFileName(SelectedTask.ReleaseMacroFilePath);

    public string SelectedHotkey
    {
        get;
        set
        {
            if (!string.Equals(field, value, StringComparison.Ordinal))
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = string.Empty;

    public string SelectedLastTriggeredText => SelectedTask?.LastTriggeredTime?.ToLocalTime().ToString("G", LocalizationService.CurrentCulture)
        ?? Localize("Never");

    public string SelectedStatusText => string.IsNullOrWhiteSpace(SelectedTask?.LastStatus)
        ? Localize("StatusPlaceholder")
        : SelectedTask.LastStatus;

    public ShortcutViewModel(
        IManageShortcut manageShortcut,
        IShortcutService shortcutService,
        IDialogService dialogService,
        IGlobalHotkeyService hotkeyService,
        ILocalizationService localizationService,
        IShortcutHotkeyNormalizer hotkeyNormalizer,
        IWindowManager? windowManager = null,
        IUiDispatcher? uiDispatcher = null)
        : base(localizationService, dialogService, "Shortcut", uiDispatcher)
    {
        _manageShortcut = manageShortcut ?? throw new ArgumentNullException(nameof(manageShortcut));
        _shortcutService = shortcutService ?? throw new ArgumentNullException(nameof(shortcutService));
        GlobalHotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _hotkeyNormalizer = hotkeyNormalizer ?? throw new ArgumentNullException(nameof(hotkeyNormalizer));
        _windowManager = windowManager;

        _shortcutService.ShortcutStarting += OnShortcutStarting;
        _shortcutService.ShortcutExecuted += OnShortcutExecuted;
        _shortcutService.Tasks?.CollectionChanged += OnServiceTasksChanged;
    }

    private protected override Task<TaskCollectionResult<ShortcutTask>> ListTasksAsync(CancellationToken cancellationToken) =>
        _manageShortcut.ListAsync(cancellationToken);

    private protected override Guid GetTaskId(ShortcutTask task) => task.Id;

    private protected override ShortcutTask CreateTask(string name) => new() { Name = name };

    private protected override Task AddToStoreAsync(ShortcutTask task, long scopeGeneration, CancellationToken cancellationToken) =>
        _manageShortcut.AddAsync(task, scopeGeneration, cancellationToken);

    private protected override Task UpdateInStoreAsync(ShortcutTask task, long scopeGeneration, CancellationToken cancellationToken) =>
        _manageShortcut.UpdateAsync(task, scopeGeneration, cancellationToken);

    private protected override Task RemoveFromStoreAsync(Guid id, long scopeGeneration, CancellationToken cancellationToken) =>
        _manageShortcut.RemoveAsync(new TaskRequest(id, ExpectedScopeGeneration: scopeGeneration), cancellationToken);

    private protected override void OnSelectedTaskChanged()
    {
        SelectedHotkey = string.Empty;
        OnPropertyChanged(nameof(SelectedMacroFilePath));
        OnPropertyChanged(nameof(SelectedMacroFileName));
        OnPropertyChanged(nameof(SelectedReleaseMacroFilePath));
        OnPropertyChanged(nameof(SelectedReleaseMacroFileName));
        OnPropertyChanged(nameof(SelectedMacroLabel));
        OnPropertyChanged(nameof(SelectedHotkey));
        OnSelectedTaskStatusChanged();
    }

    private protected override void OnSelectedTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShortcutTaskEditor.LastTriggeredTime) or nameof(ShortcutTaskEditor.LastStatus))
        {
            PostToUiThread(OnSelectedTaskStatusChanged);
        }
        if (string.Equals(e.PropertyName, nameof(ShortcutTaskEditor.RunOnPressAndRelease), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(SelectedMacroLabel));
        }
        if (string.Equals(e.PropertyName, nameof(ShortcutTaskEditor.MacroFilePath), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(SelectedMacroFilePath));
            OnPropertyChanged(nameof(SelectedMacroFileName));
        }
        if (string.Equals(e.PropertyName, nameof(ShortcutTaskEditor.ReleaseMacroFilePath), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(SelectedReleaseMacroFilePath));
            OnPropertyChanged(nameof(SelectedReleaseMacroFileName));
        }
    }

    private protected override void OnCultureChangedOnUiThread()
    {
        OnPropertyChanged(nameof(SelectedMacroFileName));
        OnPropertyChanged(nameof(SelectedReleaseMacroFileName));
        OnPropertyChanged(nameof(SelectedMacroLabel));
        OnSelectedTaskStatusChanged();
        base.OnCultureChangedOnUiThread();
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
    private Task BrowseMacroAsync() =>
        PickMacroFileAsync("OpenMacroDialogTitle", static (task, path) => task.MacroFilePath = path);

    [RelayCommand]
    private Task BrowseReleaseMacroAsync() =>
        PickMacroFileAsync("OpenReleaseMacroDialogTitle", static (task, path) => task.ReleaseMacroFilePath = path);

    [RelayCommand]
    private void AddHotkey()
    {
        if (SelectedTask is null || string.IsNullOrWhiteSpace(SelectedHotkey))
        {
            return;
        }

        _ = TryAddHotkey(SelectedHotkey, reportValidation: true);
    }

    [RelayCommand]
    private void RemoveHotkey(string? hotkey)
    {
        if (SelectedTask is null)
        {
            return;
        }

        var selectedHotkey = string.IsNullOrWhiteSpace(hotkey) ? SelectedHotkey : hotkey;
        if (SelectedTask.RemoveHotkey(selectedHotkey))
        {
            OnPropertyChanged(nameof(SelectedTask));
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
            var values = await WindowFieldValues.ListAsync(_windowManager, rule.Field).ConfigureAwait(true);
            rule.AvailableWindowValues.Clear();
            foreach (var value in values)
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

    public (bool IsValid, string ErrorMessage) ValidateShortcutHotkey(string newHotkey)
    {
        if (!_hotkeyNormalizer.TryNormalize(newHotkey, out var normalized, out var validationMessage)
            || string.IsNullOrWhiteSpace(normalized))
        {
            var message = FormatInvalidHotkeyMessage(validationMessage ?? Localize("InvalidChordFallback"));
            ReportStatus(message);
            return (false, message);
        }

        if (SelectedTask?.Hotkeys.Contains(normalized, StringComparer.OrdinalIgnoreCase) is true)
        {
            var message = FormatInvalidHotkeyMessage(Localize("TriggerAlreadyConfigured"));
            ReportStatus(message);
            return (false, message);
        }

        return (true, string.Empty);
    }

    public void OnHotkeyChanged(string newHotkey)
    {
        _ = TryAddHotkey(newHotkey, reportValidation: true);
        SelectedHotkey = string.Empty;
    }

    private string FormatInvalidHotkeyMessage(string validationMessage) => FormatLocalized("InvalidTrigger", validationMessage);

    private bool TryAddHotkey(string hotkey, bool reportValidation)
    {
        if (!_hotkeyNormalizer.TryNormalize(hotkey, out var normalized, out var validationMessage)
            || string.IsNullOrWhiteSpace(normalized))
        {
            if (reportValidation)
            {
                ReportStatus(FormatInvalidHotkeyMessage(validationMessage ?? Localize("InvalidChordFallback")));
            }

            return false;
        }

        if (SelectedTask?.AddHotkey(normalized) is not true)
        {
            if (reportValidation)
            {
                ReportStatus(FormatInvalidHotkeyMessage(Localize("TriggerAlreadyConfigured")));
            }

            return false;
        }

        OnPropertyChanged(nameof(SelectedTask));
        SelectedHotkey = string.Empty;
        return true;
    }

    private void OnShortcutStarting(object? sender, ShortcutStartingEventArgs e)
    {
        var task = e.Task;
        UiDispatcher.Post(() =>
        {
            if (IsDisposed || !_shortcutService.IsCurrentTask(task)) { return; }
            ReportStatus(FormatLocalized("StatusRunning", task.Name));
            ApplyRuntimeStatus(task);
        });
    }

    private void OnShortcutExecuted(object? sender, ShortcutExecutedEventArgs e)
    {
        UiDispatcher.Post(() =>
        {
            if (IsDisposed || !_shortcutService.IsCurrentTask(e.Task)) { return; }
            ReportStatus(e.Success
                ? FormatLocalized("StatusCompleted", e.Task.Name)
                : FormatLocalized("StatusFailed", e.Task.Name, e.Message));
            ApplyRuntimeStatus(e.Task);
        });
    }

    private void ApplyRuntimeStatus(ShortcutTask task)
    {
        if (TryGetEditor(task.Id, out var editor))
        {
            editor.SyncRuntimeStatus(task.LastTriggeredTime, task.LastStatus);
        }

        if (SelectedTask?.Id == task.Id)
        {
            OnPropertyChanged(nameof(SelectedTask));
            OnSelectedTaskStatusChanged();
        }
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
            _shortcutService.ShortcutStarting -= OnShortcutStarting;
            _shortcutService.ShortcutExecuted -= OnShortcutExecuted;
            _shortcutService.Tasks?.CollectionChanged -= OnServiceTasksChanged;
        }

        base.Dispose(disposing);
    }
}
