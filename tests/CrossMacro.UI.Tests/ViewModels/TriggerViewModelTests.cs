
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class TriggerViewModelTests
{
    [Fact]
    public async Task ManagedAddTask_CommitsEditorAndSelectionOnlyAfterServiceCompletes()
    {
        var manager = Substitute.For<IManageTrigger>();
        var triggerService = Substitute.For<ITriggerService>();
        var dialogService = Substitute.For<IDialogService>();
        var localizationService = Substitute.For<ILocalizationService>();
        var addCompletion = new TaskCompletionSource<TriggerTask>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = triggerService.Tasks.Returns(new ObservableCollection<TriggerTask>());
        _ = triggerService.LoadAsync().Returns(Task.CompletedTask);
        _ = manager.AddAsync(Arg.Any<TriggerTask>(), Arg.Any<CancellationToken>()).Returns(addCompletion.Task);
        TriggerTask? addedTask = null;
        manager.When(x => x.AddAsync(Arg.Any<TriggerTask>(), Arg.Any<CancellationToken>()))
            .Do(call => addedTask = call.Arg<TriggerTask>());
        var viewModel = new TriggerViewModel(manager, triggerService, profileManager: null, dialogService, localizationService, windowManager: null);
        await viewModel.InitializationTask;

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
            windowManager: null);
        await viewModel.InitializationTask;
        var editor = viewModel.Tasks.Single();

        await viewModel.TaskEnabledChangedCommand.ExecuteAsync(editor);

        _ = await manager.Received(1).SetEnabledAsync(Arg.Is<TaskRequest>(request =>
            request.Id == task.Id && request.Enabled == editor.IsEnabled));
        triggerService.DidNotReceive().SetTaskEnabled(Arg.Any<Guid>(), Arg.Any<bool>());
        await triggerService.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task Construction_WhenProfileRuntimeAlreadyLoaded_SkipsRedundantLoad()
    {
        var triggerService = Substitute.For<ITriggerService>();
        _ = triggerService.Tasks.Returns(new ObservableCollection<TriggerTask>());
        _ = triggerService.LoadAsync().Returns(Task.CompletedTask);
        var profileManager = Substitute.For<IProfileManager>();
        var profileRuntimeState = Substitute.For<IProfileRuntimeState>();
        _ = profileRuntimeState.IsInitialized.Returns(true);
        var dialogService = Substitute.For<IDialogService>();
        var localizationService = Substitute.For<ILocalizationService>();
        var viewModel = new TriggerViewModel(
            triggerService,
            profileManager,
            dialogService,
            localizationService,
            windowManager: null,
            profileRuntimeState);

        await viewModel.InitializationTask;

        await triggerService.DidNotReceive().LoadAsync();
        triggerService.Received(1).Start();
        viewModel.Dispose();
    }
}
