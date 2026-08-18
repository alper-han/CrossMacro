
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class ShortcutViewModelTests : IDisposable
{
    private readonly IShortcutService _shortcutService;
    private readonly IDialogService _dialogService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ILocalizationService _localizationService;
    private readonly ShortcutViewModel _viewModel;

    public ShortcutViewModelTests()
    {
        _shortcutService = Substitute.For<IShortcutService>();
        _dialogService = Substitute.For<IDialogService>();
        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
        _localizationService = Substitute.For<ILocalizationService>();
        _ = _localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _ = _localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "Shortcut_ItemsText" => "[Shortcut_ItemsText] {0}",
            "Shortcut_NoFileSelected" => "[Shortcut_NoFileSelected]",
            "Shortcut_StatusInitFailed" => "[Shortcut_StatusInitFailed] {0}",
            "Shortcut_DefaultTaskName" => "[Shortcut_DefaultTaskName] {0}",
            "Shortcut_DeleteTitle" => "[Shortcut_DeleteTitle]",
            "Shortcut_DeleteMessage" => "[Shortcut_DeleteMessage] {0}",
            "Shortcut_StatusSaveFailed" => "[Shortcut_StatusSaveFailed] {0}",
            "Shortcut_SaveFailedTitle" => "[Shortcut_SaveFailedTitle]",
            "Shortcut_OpenMacroDialogFilter" => "[Shortcut_OpenMacroDialogFilter]",
            "Shortcut_OpenMacroDialogTitle" => "[Shortcut_OpenMacroDialogTitle]",
            "Shortcut_StatusRunning" => "[Shortcut_StatusRunning] {0}",
            "Shortcut_StatusCompleted" => "[Shortcut_StatusCompleted] {0}",
            "Shortcut_StatusFailed" => "[Shortcut_StatusFailed] {0} | {1}",
            "Shortcut_StatusChangesSaved" => "[Shortcut_StatusChangesSaved]",
            _ => call.Arg<string>(),
        });

        _ = _shortcutService.Tasks.Returns(new ObservableCollection<ShortcutTask>());

        _viewModel = new ShortcutViewModel(_shortcutService, _dialogService, _hotkeyService, _localizationService);
    }

    public void Dispose()
    {
        _viewModel.Dispose();
    }

    [Fact]
    public void Construction_ExposesSuppliedHotkeyAndLocalizationServices()
    {
        _ = _viewModel.GlobalHotkeyService.Should().BeSameAs(_hotkeyService);
        _ = _viewModel.LocalizationService.Should().BeSameAs(_localizationService);
    }

    [Fact]
    public async Task Construction_LoadsAndStartsService()
    {
        await _viewModel.InitializationTask;

        await _shortcutService.Received(1).LoadAsync();
        _shortcutService.Received(1).Start();
    }

    [Fact]
    public async Task Construction_WhenProfileRuntimeAlreadyLoaded_SkipsRedundantLoad()
    {
        var shortcutService = Substitute.For<IShortcutService>();
        _ = shortcutService.Tasks.Returns(new ObservableCollection<ShortcutTask>());
        _ = shortcutService.LoadAsync().Returns(Task.CompletedTask);
        var profileRuntimeState = Substitute.For<IProfileRuntimeState>();
        _ = profileRuntimeState.IsInitialized.Returns(true);
        using var viewModel = new ShortcutViewModel(
            shortcutService,
            _dialogService,
            _hotkeyService,
            _localizationService,
            profileRuntimeState);

        await viewModel.InitializationTask;

        await shortcutService.DidNotReceive().LoadAsync();
        shortcutService.Received(1).Start();
    }

    [Fact]
    public async Task Construction_WhenLoadFails_ReportsStatusAndDoesNotThrow()
    {
        // Arrange
        var failingShortcutService = Substitute.For<IShortcutService>();
        _ = failingShortcutService.Tasks.Returns(new ObservableCollection<ShortcutTask>());

        var loadTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = failingShortcutService.LoadAsync().Returns(_ => loadTcs.Task);

        var statusTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var vm = new ShortcutViewModel(failingShortcutService, _dialogService, _hotkeyService, _localizationService);
        vm.StatusChanged += (_, status) => statusTcs.TrySetResult(status);
        _ = loadTcs.TrySetException(new InvalidOperationException("load failed"));
        await vm.InitializationTask;
        var statusMessage = await statusTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        _ = statusMessage.Should().Contain("[Shortcut_StatusInitFailed]");
        _ = statusMessage.Should().Contain("load failed");
        failingShortcutService.DidNotReceive().Start();
    }

    [Fact]
    public void CultureChanged_RaisesLocalizedComputedProperties()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        _ = localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _ = localizationService["Shortcut_ItemsText"].Returns("{0} items");
        _ = localizationService["Shortcut_NoFileSelected"].Returns("No file selected");
        var vm = new ShortcutViewModel(_shortcutService, _dialogService, _hotkeyService, localizationService);
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        localizationService.CultureChanged += Raise.Event<EventHandler>(localizationService, EventArgs.Empty);

        _ = changedProperties.Should().Contain(nameof(ShortcutViewModel.TaskCountText));
        _ = changedProperties.Should().Contain(nameof(ShortcutViewModel.SelectedMacroFileName));
        _ = changedProperties.Should().Contain(nameof(ShortcutViewModel.SelectedTask));
    }

    [Fact]
    public void AddTask_CreatesAndSelectsTask()
    {
        // Act
        _viewModel.AddTaskCommand.Execute(parameter: null);

        // Assert
        _shortcutService.Received(1).AddTask(Arg.Any<ShortcutTask>());
        _ = _viewModel.SelectedTask.Should().NotBeNull();
        _ = _viewModel.SelectedTask!.Name.Should().Contain("[Shortcut_DefaultTaskName]");
    }

    [Fact]
    public async Task ManagedAddTask_CommitsEditorAndSelectionOnlyAfterServiceCompletes()
    {
        var manager = Substitute.For<IManageShortcut>();
        var addCompletion = new TaskCompletionSource<ShortcutTask>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = manager.AddAsync(Arg.Any<ShortcutTask>(), Arg.Any<CancellationToken>()).Returns(addCompletion.Task);
        ShortcutTask? addedTask = null;
        manager.When(x => x.AddAsync(Arg.Any<ShortcutTask>(), Arg.Any<CancellationToken>()))
            .Do(call => addedTask = call.Arg<ShortcutTask>());
        var viewModel = new ShortcutViewModel(manager, _shortcutService, _dialogService, _hotkeyService, _localizationService);
        await viewModel.InitializationTask;

        var add = viewModel.AddTaskCommand.ExecuteAsync(parameter: null);

        _ = viewModel.Tasks.Should().BeEmpty();
        _ = viewModel.SelectedTask.Should().BeNull();

        _shortcutService.Tasks.Add(addedTask!);
        _ = addCompletion.TrySetResult(addedTask!);
        await add;

        _ = viewModel.Tasks.Should().ContainSingle();
        _ = viewModel.SelectedTask.Should().BeSameAs(viewModel.Tasks.Single());
    }

    [Fact]
    public void SelectedLastTriggeredText_DisplaysUtcRuntimeValueAsLocalTime()
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        _ = _localizationService.CurrentCulture.Returns(culture);
        var task = new ShortcutTaskEditor
        {
            LastTriggeredTime = new DateTime(2026, 1, 1, 7, 0, 0, DateTimeKind.Utc),
        };

        _viewModel.SelectedTask = task;

        _ = _viewModel.SelectedLastTriggeredText.Should().Be(task.LastTriggeredTime.Value.ToLocalTime().ToString("G", culture));
    }

    [Fact]
    public async Task RemoveTask_WhenConfirmed_RemovesTask()
    {
        // Arrange
        var task = new ShortcutTask();
        _shortcutService.Tasks.Add(task);
        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        var editor = _viewModel.Tasks.Single();

        // Act
        await _viewModel.RemoveTaskCommand.ExecuteAsync(editor);

        // Assert
        _shortcutService.Received(1).RemoveTask(task.Id);
        _ = _shortcutService.Received(1).SaveAsync();
    }

    [Fact]
    public async Task RemoveTask_WhenSaveFails_ReportsStatusAndShowsMessage()
    {
        // Arrange
        var task = new ShortcutTask();
        _shortcutService.Tasks.Add(task);
        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _shortcutService.When(x => x.RemoveTask(task.Id)).Do(_ => _shortcutService.Tasks.Remove(task));
        _shortcutService.When(x => x.AddTask(task)).Do(_ => _shortcutService.Tasks.Add(task));
        _ = _shortcutService.SaveAsync().Returns(Task.FromException(new InvalidOperationException("disk full")));
        var editor = _viewModel.Tasks.Single();

        var statusTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _viewModel.StatusChanged += (_, status) =>
        {
            if (status.Contains("disk full", StringComparison.OrdinalIgnoreCase))
            {
                statusTcs.TrySetResult(status);
            }
        };

        // Act
        await _viewModel.RemoveTaskCommand.ExecuteAsync(editor);
        var status = await statusTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        _ = status.Should().Contain("[Shortcut_StatusSaveFailed]");
        _ = status.Should().Contain("disk full");
        _ = _shortcutService.Tasks.Should().Contain(task);
        await _dialogService.Received(1).ShowMessageAsync(
            "[Shortcut_SaveFailedTitle]",
            Arg.Is<string>(s => s.Contains("disk full")),
            Arg.Any<string>());
    }

    [Fact]
    public async Task RemoveTask_WhenCancelled_DoesNotRemove()
    {
        // Arrange
        var task = new ShortcutTask();
        _shortcutService.Tasks.Add(task);
        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(false));
        var editor = _viewModel.Tasks.Single();

        // Act
        await _viewModel.RemoveTaskCommand.ExecuteAsync(editor);

        // Assert
        _shortcutService.DidNotReceive().RemoveTask(Arg.Any<System.Guid>());
    }

    [Fact]
    public void OnHotkeyChanged_UpdatesSelectedTask()
    {
        // Arrange
        var task = new ShortcutTaskEditor();
        _viewModel.SelectedTask = task;

        // Act
        _viewModel.OnHotkeyChanged("F9");

        // Assert
        _ = task.HotkeyString.Should().Be("F9");
        _ = _viewModel.SelectedHotkeyString.Should().Be("F9");
    }

    [Fact]
    public void AddAndRemoveWindowRule_UpdatesTheSelectedShortcutEditor()
    {
        var task = new ShortcutTaskEditor();
        _viewModel.SelectedTask = task;

        _viewModel.AddWindowRuleCommand.Execute(parameter: null);

        var rule = task.WindowRules.Should().ContainSingle().Which;
        _viewModel.RemoveWindowRuleCommand.Execute(rule);

        _ = task.WindowRules.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshWindowRuleValues_LoadsDistinctValuesForTheRuleField()
    {
        var windowManager = Substitute.For<IWindowManager>();
        _ = windowManager.GetWindowsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<WindowInfo>>(
        [
            new WindowInfo { Class = "org.mozilla.firefox", Title = "Firefox", ProcessName = "firefox" },
            new WindowInfo { Class = "org.chromium.Chromium", Title = "Chromium", ProcessName = "chromium" },
            new WindowInfo { Class = "org.mozilla.firefox", Title = "Firefox Private", ProcessName = "firefox" },
        ]));
        using var viewModel = new ShortcutViewModel(
            _shortcutService,
            _dialogService,
            _hotkeyService,
            _localizationService,
            windowManager: windowManager);
        var task = new ShortcutTaskEditor();
        task.AddWindowRule();
        var rule = task.WindowRules.Single();
        viewModel.SelectedTask = task;

        await viewModel.RefreshWindowRuleValuesCommand.ExecuteAsync(rule);

        _ = rule.AvailableWindowValues.Should().Equal("org.chromium.Chromium", "org.mozilla.firefox");
        await windowManager.Received(1).GetWindowsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshWindowRuleValues_UsesTheConfiguredFieldAndPickerUpdatesValue()
    {
        var windowManager = Substitute.For<IWindowManager>();
        _ = windowManager.GetWindowsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<WindowInfo>>(
        [
            new WindowInfo { Title = "Firefox - Work", ProcessName = "firefox" },
            new WindowInfo { Title = "Chromium - Docs", ProcessName = "chromium" },
        ]));
        using var viewModel = new ShortcutViewModel(
            _shortcutService,
            _dialogService,
            _hotkeyService,
            _localizationService,
            windowManager: windowManager);
        var task = new ShortcutTaskEditor();
        task.AddWindowRule();
        var rule = task.WindowRules.Single();
        rule.Field = TriggerField.ProcessName;
        viewModel.SelectedTask = task;

        await viewModel.RefreshWindowRuleValuesCommand.ExecuteAsync(rule);
        rule.SelectedWindowValue = "chromium";

        _ = rule.AvailableWindowValues.Should().Equal("chromium", "firefox");
        _ = rule.Value.Should().Be("chromium");
    }

    [Fact]
    public void WindowRuleFieldChange_ClearsPreviouslyFetchedValues()
    {
        var rule = new ShortcutWindowRuleEditor();
        rule.AvailableWindowValues.Add("org.mozilla.firefox");

        rule.Field = TriggerField.ProcessName;

        _ = rule.AvailableWindowValues.Should().BeEmpty();
    }

    [Fact]
    public async Task TaskEnabledChangedCommand_WhenToggleChanges_PersistsTasks()
    {
        // Arrange
        var task = new ShortcutTask
        {
            MacroFilePath = "/tmp/sample.macro",
            HotkeyString = "F9",
            IsEnabled = true,
        };
        _shortcutService.Tasks.Add(task);
        var editor = _viewModel.Tasks.Single();

        // Act
        await _viewModel.TaskEnabledChangedCommand.ExecuteAsync(editor);

        // Assert
        _shortcutService.Received(1).SetTaskEnabled(task.Id, enabled: true);
        await _shortcutService.Received(1).SaveAsync();
    }

    [Fact]
    public async Task TaskEnabledChangedCommand_WhenManaged_PersistsTheToggledTask()
    {
        var task = new ShortcutTask { IsEnabled = true, MacroFilePath = "macro", HotkeyString = "F9" };
        _shortcutService.Tasks.Add(task);
        var manager = Substitute.For<IManageShortcut>();
        var viewModel = new ShortcutViewModel(manager, _shortcutService, _dialogService, _hotkeyService, _localizationService);
        await viewModel.InitializationTask;
        var editor = viewModel.Tasks.Single();
        viewModel.SelectedTask = editor;

        await viewModel.TaskEnabledChangedCommand.ExecuteAsync(editor);

        _ = await manager.Received(1).SetEnabledAsync(Arg.Is<TaskRequest>(request =>
            request.Id == task.Id && request.Enabled == editor.IsEnabled));
        _shortcutService.DidNotReceive().SetTaskEnabled(Arg.Any<Guid>(), Arg.Any<bool>());
        await _shortcutService.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task BrowseMacro_WhenCancelled_KeepsCurrentPath()
    {
        // Arrange
        var task = new ShortcutTaskEditor { MacroFilePath = "existing.macro" };
        _viewModel.SelectedTask = task;
        _ = _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>(null));

        // Act
        await _viewModel.BrowseMacroCommand.ExecuteAsync(parameter: null);

        // Assert
        _ = task.MacroFilePath.Should().Be("existing.macro");
    }

    [Fact]
    public void SelectTask_WhenSameTaskSelected_TogglesSelectionOff()
    {
        // Arrange
        var task = new ShortcutTaskEditor();
        _viewModel.SelectedTask = task;

        // Act
        _viewModel.SelectTaskCommand.Execute(task);

        // Assert
        _ = _viewModel.SelectedTask.Should().BeNull();
    }

    [Fact]
    public async Task SaveCommand_InvokesShortcutServiceSave()
    {
        // Act
        await _viewModel.SaveCommand.ExecuteAsync(parameter: null);

        // Assert
        await _shortcutService.Received(1).SaveAsync();
    }
}
