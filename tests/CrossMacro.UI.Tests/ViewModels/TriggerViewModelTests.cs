
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
        var viewModel = new TriggerViewModel(manager, triggerService, profileManager: null, dialogService, localizationService, windowManager: null, uiDispatcher: ImmediateUiDispatcher.Instance);
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
        var task = new TriggerTask { IsEnabled = true };
        _ = triggerService.Tasks.Returns(new ObservableCollection<TriggerTask> { task });
        _ = triggerService.LoadAsync().Returns(Task.CompletedTask);

        var viewModel = new TriggerViewModel(
            manager,
            triggerService,
            profileManager: null,
            dialogService,
            localizationService,
            windowManager: null, uiDispatcher: ImmediateUiDispatcher.Instance);
        await viewModel.InitializeAsync();
        var editor = viewModel.Tasks.Single();

        await viewModel.TaskEnabledChangedCommand.ExecuteAsync(editor);

        _ = await manager.Received(1).SetEnabledAsync(Arg.Is<TaskRequest>(request =>
            request.Id == task.Id && request.Enabled == editor.IsEnabled), CancellationToken.None);
        triggerService.DidNotReceive().SetTaskEnabled(Arg.Any<Guid>(), Arg.Any<bool>());
        await triggerService.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task InitializeAsync_WhenProfileRuntimeAlreadyLoaded_SkipsRedundantLoad()
    {
        var triggerService = Substitute.For<ITriggerService>();
        _ = triggerService.Tasks.Returns(new ObservableCollection<TriggerTask>());
        _ = triggerService.LoadAsync().Returns(Task.CompletedTask);
        var profileManager = Substitute.For<IProfileManager>();
        var profileRuntimeState = Substitute.For<IProfileRuntimeState>();
        _ = profileRuntimeState.IsInitialized.Returns(returnThis: true);
        var dialogService = Substitute.For<IDialogService>();
        var localizationService = Substitute.For<ILocalizationService>();
        var viewModel = new TriggerViewModel(
            UiAutomationTestComposition.Create(triggerService, alreadyLoaded: true), triggerService,
            profileManager,
            dialogService,
            localizationService,
            windowManager: null,
            profileRuntimeState, uiDispatcher: ImmediateUiDispatcher.Instance);

        await viewModel.InitializeAsync();

        await triggerService.DidNotReceive().LoadAsync();
        triggerService.DidNotReceive().Start();
        viewModel.Dispose();
    }
}
