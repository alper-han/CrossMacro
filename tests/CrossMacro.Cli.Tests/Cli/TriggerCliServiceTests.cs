using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Application.Automation;
using CrossMacro.Core.Models;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using NSubstitute;
using Xunit;

namespace CrossMacro.Cli.Tests;

public class TriggerCliServiceTests
{
    [Fact]
    public async Task ListAsync_LoadsAndReturnsTaskList()
    {
        var triggerService = Substitute.For<IManageTrigger>();
        triggerService.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>(new ObservableCollection<TriggerTask>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Trigger 1",
                Field = TriggerField.WindowClass,
                MatchMode = TriggerMatchMode.Contains,
                Value = "vscode",
                Action = TriggerAction.SwitchProfile,
                TargetProfileId = "dev",
                IsEnabled = true
            }
        }));

        var service = new TriggerCliService(triggerService);
        var result = await service.ListAsync(CancellationToken.None);

        Assert.True(result.Success);
        await triggerService.Received(1).ListAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_Add_AddsAndSavesTask()
    {
        var triggerService = Substitute.For<IManageTrigger>();
        triggerService.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>(new ObservableCollection<TriggerTask>()));
        var service = new TriggerCliService(triggerService);

        var result = await service.ExecuteAsync(
            new TriggerCliOptions(
                TriggerCliAction.Add,
                Name: "Demo Trigger",
                Field: TriggerField.WindowTitle,
                MatchMode: TriggerMatchMode.Regex,
                Value: ".*Firefox.*",
                TriggerActionVal: TriggerAction.RunMacro,
                MacroFilePath: "/tmp/demo.macro",
                FireMode: TriggerFireMode.OnEnter,
                CooldownMs: 1000,
                DebounceMs: 250,
                Enabled: true),
            CancellationToken.None);

        Assert.True(result.Success);
        var taskData = JsonSerializer.SerializeToElement(result.Data);
        Assert.Equal("WindowTitle", taskData.GetProperty("field").GetString());
        Assert.Equal("Regex", taskData.GetProperty("matchMode").GetString());
        Assert.Equal("RunMacro", taskData.GetProperty("action").GetString());
        Assert.Equal("/tmp/demo.macro", taskData.GetProperty("macroFilePath").GetString());
        Assert.Equal(1000, taskData.GetProperty("cooldownMs").GetInt32());
        Assert.Equal(250, taskData.GetProperty("debounceMs").GetInt32());

        await triggerService.Received(1).AddAsync(Arg.Is<TriggerTask>(task =>
            task.Name == "Demo Trigger"
            && task.Field == TriggerField.WindowTitle
            && task.MatchMode == TriggerMatchMode.Regex
            && task.Value == ".*Firefox.*"
            && task.Action == TriggerAction.RunMacro
            && task.MacroFilePath == "/tmp/demo.macro"
            && task.FireMode == TriggerFireMode.OnEnter
            && task.CooldownMs == 1000
            && task.DebounceMs == 250
            && task.IsEnabled));
    }

    [Fact]
    public async Task ExecuteAsync_EditExistingTask_UpdatesAndSavesTask()
    {
        var id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var task = new TriggerTask
        {
            Id = id,
            Name = "Old Name",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Equals,
            Value = "old",
            Action = TriggerAction.SwitchProfile,
            TargetProfileId = "old-profile"
        };
        var triggerService = Substitute.For<IManageTrigger>();
        triggerService.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>(new ObservableCollection<TriggerTask> { task }));
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
        var taskData = JsonSerializer.SerializeToElement(result.Data);
        Assert.Equal("new", taskData.GetProperty("value").GetString());
        Assert.Equal(500, taskData.GetProperty("cooldownMs").GetInt32());

        await triggerService.Received(1).UpdateAsync(Arg.Is<TriggerTask>(updated =>
            updated.Id == id
            && updated.Name == "New Name"
            && updated.Value == "new"
            && updated.CooldownMs == 500));
    }

    [Fact]
    public async Task ExecuteAsync_RemoveMissingTask_ReturnsInvalidArguments()
    {
        var triggerService = Substitute.For<IManageTrigger>();
        triggerService.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>(new ObservableCollection<TriggerTask>()));
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
        var id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var triggerService = Substitute.For<IManageTrigger>();
        triggerService.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<TriggerTask>(new ObservableCollection<TriggerTask>
        {
            new() { Id = id, Name = "Trigger", Action = TriggerAction.SwitchProfile, TargetProfileId = "dev", IsEnabled = true }
        }));
        var service = new TriggerCliService(triggerService);

        var result = await service.ExecuteAsync(
            new TriggerCliOptions(TriggerCliAction.Disable, TaskId: id.ToString()),
            CancellationToken.None);

        Assert.True(result.Success);
        await triggerService.Received(1).SetEnabledAsync(new TaskRequest(id, false), CancellationToken.None);
    }
}
