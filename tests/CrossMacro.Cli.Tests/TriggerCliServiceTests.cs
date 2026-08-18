
namespace CrossMacro.Cli.Tests;

public sealed class TriggerCliServiceTests
{
    [Fact]
    public async Task ListAsync_LoadsAndReturnsTaskList()
    {
        var triggerService = Substitute.For<IManageTrigger>();
        _ = triggerService.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>(new ObservableCollection<TriggerTask>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Trigger 1",
                Field = TriggerField.WindowClass,
                MatchMode = TriggerMatchMode.Contains,
                Value = "vscode",
                Action = TriggerOperation.SwitchProfile,
                TargetProfileId = "dev",
                IsEnabled = true,
            },
        }));

        var service = new TriggerCliService(triggerService);
        var result = await service.ListAsync(CancellationToken.None);

        Assert.True(result.Success);
        _ = await triggerService.Received(1).ListAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_Add_AddsAndSavesTask()
    {
        var triggerService = Substitute.For<IManageTrigger>();
        _ = triggerService.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>(new ObservableCollection<TriggerTask>()));
        var service = new TriggerCliService(triggerService);

        var result = await service.ExecuteAsync(
            new TriggerCliOptions(
                TriggerCliAction.Add,
                Name: "Demo Trigger",
                Field: TriggerField.WindowTitle,
                MatchMode: TriggerMatchMode.Regex,
                Value: ".*Firefox.*",
                TriggerActionVal: TriggerOperation.RunMacro,
                MacroFilePath: "/tmp/demo.macro",
                FireMode: TriggerFireMode.OnEnter,
                CooldownMs: 1000,
                DebounceMs: 250,
                Enabled: true),
            CancellationToken.None);

        Assert.True(result.Success);
        var taskData = Assert.IsType<TriggerTaskData>(result.Data);
        Assert.Equal("WindowTitle", taskData.Field);
        Assert.Equal("Regex", taskData.MatchMode);
        Assert.Equal("RunMacro", taskData.Action);
        Assert.Equal("/tmp/demo.macro", taskData.MacroFilePath);
        Assert.Equal(1000, taskData.CooldownMs);
        Assert.Equal(250, taskData.DebounceMs);

        _ = await triggerService.Received(1).AddAsync(Arg.Is<TriggerTask>(task =>
            task != null
            && task.Name == "Demo Trigger"
            && task.Field == TriggerField.WindowTitle
            && task.MatchMode == TriggerMatchMode.Regex
            && task.Value == ".*Firefox.*"
            && task.Action == TriggerOperation.RunMacro
            && task.MacroFilePath == "/tmp/demo.macro"
            && task.FireMode == TriggerFireMode.OnEnter
            && task.CooldownMs == 1000
            && task.DebounceMs == 250
            && task.IsEnabled));
    }

    [Fact]
    public async Task ExecuteAsync_EditExistingTask_UpdatesAndSavesTask()
    {
        var id = new Guid(0x33333333, 0x3333, 0x3333, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33);
        var task = new TriggerTask
        {
            Id = id,
            Name = "Old Name",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Equals,
            Value = "old",
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "old-profile",
        };
        var triggerService = Substitute.For<IManageTrigger>();
        _ = triggerService.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>(new ObservableCollection<TriggerTask> { task }));
        var service = new TriggerCliService(triggerService);

        var result = await service.ExecuteAsync(
            new TriggerCliOptions(
                TriggerCliAction.Edit,
                TaskId: id.ToString(),
                Name: "New Name",
                Value: "new",
                CooldownMs: 500),
            CancellationToken.None);

        Assert.True(result.Success);
        var taskData = Assert.IsType<TriggerTaskData>(result.Data);
        Assert.Equal("new", taskData.Value);
        Assert.Equal(500, taskData.CooldownMs);

        _ = await triggerService.Received(1).UpdateAsync(Arg.Is<TriggerTask>(updated =>
            updated != null
            && updated.Id == id
            && updated.Name == "New Name"
            && updated.Value == "new"
            && updated.CooldownMs == 500));
    }

    [Fact]
    public async Task ExecuteAsync_RemoveMissingTask_ReturnsInvalidArguments()
    {
        var triggerService = Substitute.For<IManageTrigger>();
        _ = triggerService.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>(new ObservableCollection<TriggerTask>()));
        var service = new TriggerCliService(triggerService);

        var result = await service.ExecuteAsync(
            new TriggerCliOptions(TriggerCliAction.Remove, TaskId: "33333333-3333-3333-3333-333333333333"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_DisableExistingTask_SavesMutation()
    {
        var id = new Guid(0x33333333, 0x3333, 0x3333, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33);
        var triggerService = Substitute.For<IManageTrigger>();
        _ = triggerService.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>(new ObservableCollection<TriggerTask>
        {
            new() { Id = id, Name = "Trigger", Action = TriggerOperation.SwitchProfile, TargetProfileId = "dev", IsEnabled = true },
        }));
        var service = new TriggerCliService(triggerService);

        var result = await service.ExecuteAsync(
            new TriggerCliOptions(TriggerCliAction.Disable, TaskId: id.ToString()),
            CancellationToken.None);

        Assert.True(result.Success);
        _ = await triggerService.Received(1).SetEnabledAsync(new TaskRequest(id, Enabled: false), CancellationToken.None);
    }
}
