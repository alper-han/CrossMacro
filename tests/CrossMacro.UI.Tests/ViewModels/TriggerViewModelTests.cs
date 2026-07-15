
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class TriggerViewModelTests
{
    [Fact]
    public async Task TaskEnabledChangedCommand_WhenManaged_PersistsTheToggledTask()
    {
        var manager = Substitute.For<IManageTrigger>();
        var triggerService = Substitute.For<ITriggerService>();
        var dialogService = Substitute.For<IDialogService>();
        var localizationService = Substitute.For<ILocalizationService>();
        var task = new TriggerTask { IsEnabled = true };
        triggerService.Tasks.Returns(new ObservableCollection<TriggerTask> { task });
        triggerService.LoadAsync().Returns(Task.CompletedTask);

        var viewModel = new TriggerViewModel(
            manager,
            triggerService,
            profileManager: null,
            dialogService,
            localizationService,
            windowManager: null);
        await viewModel.InitializationTask;

        await viewModel.TaskEnabledChangedCommand.ExecuteAsync(task);

        await manager.Received(1).SetEnabledAsync(Arg.Is<TaskRequest>(request =>
            request.Id == task.Id && request.Enabled == task.IsEnabled));
        triggerService.DidNotReceive().SetTaskEnabled(Arg.Any<Guid>(), Arg.Any<bool>());
        await triggerService.DidNotReceive().SaveAsync();
    }
}
