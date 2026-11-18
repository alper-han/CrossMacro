namespace CrossMacro.UI.Tests.ViewModels;

public sealed class TriggerViewModelTests
{
    [Fact]
    public async Task ManagedAddTask_CommitsEditorAndSelectionOnlyAfterServiceCompletes()
    {
        var manager = Substitute.For<IManageTrigger>();
        var triggerService = Substitute.For<ITriggerService>();
        _ = manager.ListAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(new TaskCollectionResult<TriggerTask>(triggerService.Tasks.ToArray(), scopeGeneration: 0)));
        var dialogService = Substitute.For<IDialogService>();
        var localizationService = Substitute.For<ILocalizationService>();
        var addCompletion = new TaskCompletionSource<TriggerTask>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = triggerService.Tasks.Returns(new ObservableCollection<TriggerTask>());
        _ = triggerService.LoadAsync().Returns(Task.CompletedTask);
        _ = manager.AddAsync(Arg.Any<TriggerTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(addCompletion.Task);
        TriggerTask? addedTask = null;
        manager.When(x => x.AddAsync(Arg.Any<TriggerTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>()))
            .Do(call => addedTask = call.Arg<TriggerTask>());
        using var viewModel = new TriggerViewModel(manager, triggerService, profileManager: null, dialogService, localizationService, windowManager: null, uiDispatcher: ImmediateUiDispatcher.Instance);
        await viewModel.InitializeAsync();

        var add = viewModel.AddTaskCommand.ExecuteAsync(parameter: null);

        _ = viewModel.Tasks.Should().BeEmpty();
        _ = viewModel.SelectedTask.Should().BeNull();

        triggerService.Tasks.Add(addedTask!);
        _ = addCompletion.TrySetResult(addedTask!);
        await add;

        _ = viewModel.Tasks.Should().ContainSingle();
        _ = viewModel.SelectedTask.Should().BeSameAs(viewModel.Tasks.Single());
    }

    [Fact]
    public async Task TaskEnabledChangedCommand_WhenManaged_PersistsTheToggledTask()
    {
        var manager = Substitute.For<IManageTrigger>();
        var triggerService = Substitute.For<ITriggerService>();
        _ = manager.ListAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(new TaskCollectionResult<TriggerTask>(triggerService.Tasks.ToArray(), scopeGeneration: 0)));
        var dialogService = Substitute.For<IDialogService>();
        var localizationService = Substitute.For<ILocalizationService>();
        var task = new TriggerTask { Field = TriggerField.None, Action = TriggerOperation.RunMacro, MacroFilePath = "macro", IsEnabled = true };
        _ = triggerService.Tasks.Returns(new ObservableCollection<TriggerTask> { task });
        _ = triggerService.LoadAsync().Returns(Task.CompletedTask);

        using var viewModel = new TriggerViewModel(
            manager,
            triggerService,
            profileManager: null,
            dialogService,
            localizationService,
            windowManager: null, uiDispatcher: ImmediateUiDispatcher.Instance);
        await viewModel.InitializeAsync();
        var editor = viewModel.Tasks.Single();

        await viewModel.TaskEnabledChangedCommand.ExecuteAsync(editor);

        _ = await manager.Received(1).UpdateAsync(Arg.Is<TriggerTask>((TriggerTask draft) =>
            draft.Id == task.Id && draft.IsEnabled && draft.Value == editor.Value && draft.Action == editor.Action && draft.MacroFilePath == editor.MacroFilePath), Arg.Is<long>(generation => generation == 0), CancellationToken.None);
        await manager.DidNotReceive().SetEnabledAsync(Arg.Any<TaskRequest>(), CancellationToken.None);
        await triggerService.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task TaskEnabledChangedCommand_WhenManaged_PersistsUnsavedEditorDraft()
    {
        var task = new TriggerTask
        {
            Field = TriggerField.WindowClass,
            Value = "window",
            Action = TriggerOperation.RunMacro,
            MacroFilePath = "old.macro",
            IsEnabled = false,
        };
        var triggerService = Substitute.For<ITriggerService>();
        _ = triggerService.Tasks.Returns(new ObservableCollection<TriggerTask> { task });
        _ = triggerService.LoadAsync().Returns(Task.CompletedTask);
        _ = triggerService.SaveAsync().Returns(Task.CompletedTask);
        var manager = UiAutomationTestComposition.Create(triggerService);
        using var viewModel = new TriggerViewModel(manager, triggerService, profileManager: null, Substitute.For<IDialogService>(), Substitute.For<ILocalizationService>(), windowManager: null, uiDispatcher: ImmediateUiDispatcher.Instance);
        await viewModel.InitializeAsync();
        var editor = viewModel.Tasks.Single();
        editor.MacroFilePath = "edited.macro";
        editor.Value = "edited-window";
        editor.IsEnabled = true;

        await viewModel.TaskEnabledChangedCommand.ExecuteAsync(editor);

        _ = viewModel.Tasks.Single().IsEnabled.Should().BeTrue();
        _ = viewModel.Tasks.Single().Value.Should().Be("edited-window");
        _ = viewModel.Tasks.Single().MacroFilePath.Should().Be("edited.macro");
        _ = task.IsEnabled.Should().BeTrue();
        _ = task.Value.Should().Be("edited-window");
        _ = task.MacroFilePath.Should().Be("edited.macro");
        viewModel.Dispose();
    }

    [Fact]
    public async Task InitializeAsync_WhenProfileRuntimeAlreadyLoaded_SkipsRedundantLoad()
    {
        var triggerService = Substitute.For<ITriggerService>();
        _ = triggerService.Tasks.Returns(new ObservableCollection<TriggerTask>());
        _ = triggerService.LoadAsync().Returns(Task.CompletedTask);
        var profileManager = Substitute.For<IProfileManager>();
        var dialogService = Substitute.For<IDialogService>();
        var localizationService = Substitute.For<ILocalizationService>();
        using var viewModel = new TriggerViewModel(
            UiAutomationTestComposition.Create(triggerService, alreadyLoaded: true), triggerService,
            profileManager,
            dialogService,
            localizationService,
            windowManager: null,
            uiDispatcher: ImmediateUiDispatcher.Instance);

        await viewModel.InitializeAsync();

        await triggerService.DidNotReceive().LoadAsync();
        triggerService.DidNotReceive().Start();
        viewModel.Dispose();
    }
}
