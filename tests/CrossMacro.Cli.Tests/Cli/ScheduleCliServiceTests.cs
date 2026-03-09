using System;
using System.Collections.ObjectModel;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class ScheduleCliServiceTests
{
    [Fact]
    public async Task ListAsync_LoadsAndReturnsTaskList()
    {
        var scheduler = Substitute.For<ISchedulerService>();
        scheduler.Tasks.Returns(new ObservableCollection<ScheduledTask>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Task 1",
                MacroFilePath = "/tmp/a.macro",
                IsEnabled = true
            }
        });

        var service = new ScheduleCliService(scheduler);
        var result = await service.ListAsync(CancellationToken.None);

        Assert.True(result.Success);
        await scheduler.Received(1).LoadAsync();
    }

    [Fact]
    public async Task RunAsync_WithInvalidGuid_ReturnsInvalidArguments()
    {
        var scheduler = Substitute.For<ISchedulerService>();
        scheduler.Tasks.Returns(new ObservableCollection<ScheduledTask>());

        var service = new ScheduleCliService(scheduler);
        var result = await service.RunAsync("invalid-guid", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_WithMissingTask_ReturnsInvalidArguments()
    {
        var scheduler = Substitute.For<ISchedulerService>();
        scheduler.Tasks.Returns(new ObservableCollection<ScheduledTask>());

        var service = new ScheduleCliService(scheduler);
        var result = await service.RunAsync("11111111-1111-1111-1111-111111111111", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        await scheduler.Received(1).LoadAsync();
    }

    [Fact]
    public async Task RunAsync_WithExistingTask_RunsTask()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var scheduler = Substitute.For<ISchedulerService>();
        scheduler.Tasks.Returns(new ObservableCollection<ScheduledTask>
        {
            new()
            {
                Id = id,
                Name = "Task 1",
                MacroFilePath = "/tmp/a.macro"
            }
        });

        var service = new ScheduleCliService(scheduler);
        var result = await service.RunAsync(id.ToString(), CancellationToken.None);

        Assert.True(result.Success);
        await scheduler.Received(1).RunTaskAsync(id, CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledAfterLoad_DoesNotRunTask()
    {
        var id = Guid.NewGuid();
        var scheduler = Substitute.For<ISchedulerService>();
        using var cts = new CancellationTokenSource();

        scheduler.LoadAsync().Returns(_ =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        });

        scheduler.Tasks.Returns(new ObservableCollection<ScheduledTask>
        {
            new()
            {
                Id = id,
                Name = "Task 1",
                MacroFilePath = "/tmp/a.macro"
            }
        });

        var service = new ScheduleCliService(scheduler);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.RunAsync(id.ToString(), cts.Token));
        await scheduler.DidNotReceive().RunTaskAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
