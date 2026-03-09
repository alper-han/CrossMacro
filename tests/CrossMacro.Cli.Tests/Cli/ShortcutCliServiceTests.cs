using System;
using System.Collections.ObjectModel;
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
}
