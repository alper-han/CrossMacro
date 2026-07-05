using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class ShortcutCliServiceTests
{
    [Fact]
    public async Task ListAsync_LoadsAndReturnsTaskList()
    {
        var shortcutService = Substitute.For<IShortcutService>();
        shortcutService.Tasks.Returns(new ObservableCollection<ShortcutTask>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Shortcut 1",
                HotkeyString = "F9",
                MacroFilePath = "/tmp/a.macro",
                IsEnabled = true
            }
        });

        var service = new ShortcutCliService(shortcutService);
        var result = await service.ListAsync(CancellationToken.None);

        Assert.True(result.Success);
        await shortcutService.Received(1).LoadAsync();
    }

    [Fact]
    public async Task RunAsync_WithInvalidGuid_ReturnsInvalidArguments()
    {
        var shortcuts = Substitute.For<IShortcutService>();
        shortcuts.Tasks.Returns(new ObservableCollection<ShortcutTask>());

        var service = new ShortcutCliService(shortcuts);
        var result = await service.RunAsync("invalid-guid", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_WithMissingTask_ReturnsInvalidArguments()
    {
        var shortcuts = Substitute.For<IShortcutService>();
        shortcuts.Tasks.Returns(new ObservableCollection<ShortcutTask>());

        var service = new ShortcutCliService(shortcuts);
        var result = await service.RunAsync("22222222-2222-2222-2222-222222222222", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        await shortcuts.Received(1).LoadAsync();
    }

    [Fact]
    public async Task RunAsync_WithExistingTask_RunsTask()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var shortcuts = Substitute.For<IShortcutService>();
        shortcuts.Tasks.Returns(new ObservableCollection<ShortcutTask>
        {
            new()
            {
                Id = id,
                Name = "Shortcut 1",
                MacroFilePath = "/tmp/a.macro",
                HotkeyString = "F9"
            }
        });

        var service = new ShortcutCliService(shortcuts);
        var result = await service.RunAsync(id.ToString(), CancellationToken.None);

        Assert.True(result.Success);
        await shortcuts.Received(1).RunTaskAsync(id, CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledAfterLoad_DoesNotRunTask()
    {
        var id = Guid.NewGuid();
        var shortcuts = Substitute.For<IShortcutService>();
        using var cts = new CancellationTokenSource();

        shortcuts.LoadAsync().Returns(_ =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        });

        shortcuts.Tasks.Returns(new ObservableCollection<ShortcutTask>
        {
            new()
            {
                Id = id,
                Name = "Shortcut 1",
                MacroFilePath = "/tmp/a.macro",
                HotkeyString = "F9"
            }
        });

        var service = new ShortcutCliService(shortcuts);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.RunAsync(id.ToString(), cts.Token));
        await shortcuts.DidNotReceive().RunTaskAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Add_AddsAndSavesTask()
    {
        var shortcuts = Substitute.For<IShortcutService>();
        shortcuts.Tasks.Returns(new ObservableCollection<ShortcutTask>());
        var service = new ShortcutCliService(shortcuts);

        var result = await service.ExecuteAsync(
            new ShortcutCliOptions(
                ShortcutCliAction.Add,
                Name: "Demo",
                MacroFilePath: "/tmp/demo.macro",
                Hotkey: "Ctrl+Alt+D",
                Loop: true,
                RepeatCount: 3,
                RepeatDelayMs: 250,
                Enabled: true),
            CancellationToken.None);

        Assert.True(result.Success);
        var taskData = JsonSerializer.SerializeToElement(result.Data);
        Assert.False(taskData.GetProperty("randomRepeatDelay").GetBoolean());
        shortcuts.Received(1).AddTask(Arg.Is<ShortcutTask>(task =>
            task.Name == "Demo"
            && task.MacroFilePath == "/tmp/demo.macro"
            && task.HotkeyString == "Ctrl+Alt+D"
            && task.LoopEnabled
            && task.RepeatCount == 3
            && task.RepeatDelayMs == 250
            && task.IsEnabled));
        await shortcuts.Received(1).SaveAsync();
    }

    [Fact]
    public async Task ExecuteAsync_EditExistingTask_UpdatesAndSavesTask()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var task = new ShortcutTask { Id = id, Name = "Old", MacroFilePath = "/tmp/old.macro", HotkeyString = "F7" };
        var shortcuts = Substitute.For<IShortcutService>();
        shortcuts.Tasks.Returns(new ObservableCollection<ShortcutTask> { task });
        var service = new ShortcutCliService(shortcuts);

        var result = await service.ExecuteAsync(
            new ShortcutCliOptions(
                ShortcutCliAction.Edit,
                TaskId: id.ToString(),
                Name: "New",
                RepeatDelayMinMs: 100,
                RepeatDelayMaxMs: 200),
            CancellationToken.None);

        Assert.True(result.Success);
        var taskData = JsonSerializer.SerializeToElement(result.Data);
        Assert.True(taskData.GetProperty("randomRepeatDelay").GetBoolean());
        Assert.Equal(100, taskData.GetProperty("repeatDelayMinMs").GetInt32());
        Assert.Equal(200, taskData.GetProperty("repeatDelayMaxMs").GetInt32());
        shortcuts.Received(1).UpdateTask(Arg.Is<ShortcutTask>(updated =>
            updated.Id == id
            && updated.Name == "New"
            && updated.UseRandomRepeatDelay
            && updated.RepeatDelayMinMs == 100
            && updated.RepeatDelayMaxMs == 200));
        await shortcuts.Received(1).SaveAsync();
    }

    [Fact]
    public async Task ExecuteAsync_Bind_UpdatesHotkeyAndSavesTask()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var task = new ShortcutTask { Id = id, Name = "Shortcut", MacroFilePath = "/tmp/a.macro", HotkeyString = "F7" };
        var shortcuts = Substitute.For<IShortcutService>();
        shortcuts.Tasks.Returns(new ObservableCollection<ShortcutTask> { task });
        var service = new ShortcutCliService(shortcuts);

        var result = await service.ExecuteAsync(
            new ShortcutCliOptions(ShortcutCliAction.Bind, TaskId: id.ToString(), Hotkey: "Ctrl+Shift+M"),
            CancellationToken.None);

        Assert.True(result.Success);
        shortcuts.Received(1).UpdateTask(Arg.Is<ShortcutTask>(updated => updated.Id == id && updated.HotkeyString == "Ctrl+Shift+M"));
        await shortcuts.Received(1).SaveAsync();
    }

    [Fact]
    public async Task ExecuteAsync_RemoveMissingTask_ReturnsInvalidArguments()
    {
        var shortcuts = Substitute.For<IShortcutService>();
        shortcuts.Tasks.Returns(new ObservableCollection<ShortcutTask>());
        var service = new ShortcutCliService(shortcuts);

        var result = await service.ExecuteAsync(
            new ShortcutCliOptions(ShortcutCliAction.Remove, TaskId: "22222222-2222-2222-2222-222222222222"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        await shortcuts.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ExecuteAsync_DisableExistingTask_SavesMutation()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var shortcuts = Substitute.For<IShortcutService>();
        shortcuts.Tasks.Returns(new ObservableCollection<ShortcutTask>
        {
            new() { Id = id, Name = "Shortcut", MacroFilePath = "/tmp/a.macro", HotkeyString = "F9" }
        });
        var service = new ShortcutCliService(shortcuts);

        var result = await service.ExecuteAsync(new ShortcutCliOptions(ShortcutCliAction.Disable, TaskId: id.ToString()), CancellationToken.None);

        Assert.True(result.Success);
        shortcuts.Received(1).SetTaskEnabled(id, false);
        await shortcuts.Received(1).SaveAsync();
    }
}
