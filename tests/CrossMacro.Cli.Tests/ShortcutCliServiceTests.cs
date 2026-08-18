
namespace CrossMacro.Cli.Tests;

public sealed class ShortcutCliServiceTests
{
    [Fact]
    public async Task ListAsync_LoadsAndReturnsTaskList()
    {
        var shortcutService = Substitute.For<IManageShortcut>();
        _ = shortcutService.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>(new ObservableCollection<ShortcutTask>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Shortcut 1",
                HotkeyString = "F9",
                MacroFilePath = "/tmp/a.macro",
                IsEnabled = true,
            },
        }));

        var service = new ShortcutCliService(shortcutService);
        var result = await service.ListAsync(CancellationToken.None);

        Assert.True(result.Success);
        _ = await shortcutService.Received(1).ListAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_WithInvalidGuid_ReturnsInvalidArguments()
    {
        var shortcuts = Substitute.For<IManageShortcut>();
        _ = shortcuts.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>(new ObservableCollection<ShortcutTask>()));

        var service = new ShortcutCliService(shortcuts);
        var result = await service.RunAsync("invalid-guid", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_WithMissingTask_ReturnsInvalidArguments()
    {
        var shortcuts = Substitute.For<IManageShortcut>();
        _ = shortcuts.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>(new ObservableCollection<ShortcutTask>()));

        var service = new ShortcutCliService(shortcuts);
        var result = await service.RunAsync("22222222-2222-2222-2222-222222222222", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        _ = await shortcuts.Received(1).ListAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_WithExistingTask_RunsTask()
    {
        var id = new Guid(0x22222222, 0x2222, 0x2222, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22);
        var shortcuts = Substitute.For<IManageShortcut>();
        _ = shortcuts.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>(new ObservableCollection<ShortcutTask>
        {
            new()
            {
                Id = id,
                Name = "Shortcut 1",
                MacroFilePath = "/tmp/a.macro",
                HotkeyString = "F9",
            },
        }));

        var service = new ShortcutCliService(shortcuts);
        var result = await service.RunAsync(id.ToString(), CancellationToken.None);

        Assert.True(result.Success);
        await shortcuts.Received(1).RunAsync(new TaskRequest(id), CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledAfterLoad_DoesNotRunTask()
    {
        var id = Guid.NewGuid();
        var shortcuts = Substitute.For<IManageShortcut>();
        using var cts = new CancellationTokenSource();
        var tasks = new ObservableCollection<ShortcutTask>
        {
            new()
            {
                Id = id,
                Name = "Shortcut 1",
                MacroFilePath = "/tmp/a.macro",
                HotkeyString = "F9",
            },
        };

        _ = shortcuts.ListAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            cts.Cancel();
            return Task.FromResult(new TaskCollectionResult<ShortcutTask>(tasks));
        });

        var service = new ShortcutCliService(shortcuts);

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => service.RunAsync(id.ToString(), cts.Token));
        await shortcuts.DidNotReceive().RunAsync(Arg.Any<TaskRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Add_AddsAndSavesTask()
    {
        var shortcuts = Substitute.For<IManageShortcut>();
        _ = shortcuts.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>(new ObservableCollection<ShortcutTask>()));
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
        var taskData = Assert.IsType<ShortcutTaskData>(result.Data);
        Assert.False(taskData.RandomRepeatDelay);
        _ = await shortcuts.Received(1).AddAsync(Arg.Is<ShortcutTask>(task =>
            task != null
            && task.Name == "Demo"
            && task.MacroFilePath == "/tmp/demo.macro"
            && task.HotkeyString == "Ctrl+Alt+D"
            && task.LoopEnabled
            && task.RepeatCount == 3
            && task.RepeatDelayMs == 250
            && task.IsEnabled));
    }

    [Fact]
    public async Task ExecuteAsync_EditExistingTask_UpdatesAndSavesTask()
    {
        var id = new Guid(0x22222222, 0x2222, 0x2222, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22);
        var task = new ShortcutTask { Id = id, Name = "Old", MacroFilePath = "/tmp/old.macro", HotkeyString = "F7" };
        var shortcuts = Substitute.For<IManageShortcut>();
        _ = shortcuts.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>(new ObservableCollection<ShortcutTask> { task }));
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
        var taskData = Assert.IsType<ShortcutTaskData>(result.Data);
        Assert.True(taskData.RandomRepeatDelay);
        Assert.Equal(100, taskData.RepeatDelayMinMs);
        Assert.Equal(200, taskData.RepeatDelayMaxMs);
        _ = await shortcuts.Received(1).UpdateAsync(Arg.Is<ShortcutTask>(updated =>
            updated != null
            && updated.Id == id
            && updated.Name == "New"
            && updated.UseRandomRepeatDelay
            && updated.RepeatDelayMinMs == 100
            && updated.RepeatDelayMaxMs == 200));
    }

    [Fact]
    public async Task ExecuteAsync_EditWindowRules_ReplacesExistingRulesAndReturnsThem()
    {
        var id = Guid.NewGuid();
        var task = new ShortcutTask
        {
            Id = id,
            Name = "Browser Shortcut",
            MacroFilePath = "/tmp/browser.macro",
            HotkeyString = "F7",
        };
        task.WindowRules.Add(new ShortcutWindowRule
        {
            Field = TriggerField.WindowTitle,
            MatchMode = TriggerMatchMode.Contains,
            Value = "Old",
        });
        var shortcuts = Substitute.For<IManageShortcut>();
        _ = shortcuts.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>(new ObservableCollection<ShortcutTask> { task }));
        var service = new ShortcutCliService(shortcuts);

        var result = await service.ExecuteAsync(
            new ShortcutCliOptions(
                ShortcutCliAction.Edit,
                TaskId: id.ToString(),
                WindowRules:
                [
                    new ShortcutWindowRule
                    {
                        Field = TriggerField.WindowClass,
                        MatchMode = TriggerMatchMode.Equals,
                        Value = "org.mozilla.firefox",
                    },
                ]),
            CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<ShortcutTaskData>(result.Data);
        Assert.Collection(
            data.WindowRules,
            rule =>
            {
                Assert.Equal(TriggerField.WindowClass, rule.Field);
                Assert.Equal(TriggerMatchMode.Equals, rule.MatchMode);
                Assert.Equal("org.mozilla.firefox", rule.Value);
            });
        _ = task.WindowRules.Should().ContainSingle()
            .Which.Value.Should().Be("org.mozilla.firefox");
        _ = await shortcuts.Received(1).UpdateAsync(
            Arg.Is<ShortcutTask>(updated => updated.WindowRules.Count == 1
                && updated.WindowRules.Single().Value == "org.mozilla.firefox"),
            CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_ClearWindowRules_RemovesExistingRules()
    {
        var id = Guid.NewGuid();
        var task = new ShortcutTask
        {
            Id = id,
            Name = "Browser Shortcut",
            MacroFilePath = "/tmp/browser.macro",
            HotkeyString = "F7",
        };
        task.WindowRules.Add(new ShortcutWindowRule
        {
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Contains,
            Value = "firefox",
        });
        var shortcuts = Substitute.For<IManageShortcut>();
        _ = shortcuts.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>(new ObservableCollection<ShortcutTask> { task }));
        var service = new ShortcutCliService(shortcuts);

        var result = await service.ExecuteAsync(
            new ShortcutCliOptions(ShortcutCliAction.Edit, TaskId: id.ToString(), ClearWindowRules: true),
            CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<ShortcutTaskData>(result.Data);
        Assert.Empty(data.WindowRules);
        Assert.Empty(task.WindowRules);
        _ = await shortcuts.Received(1).UpdateAsync(
            Arg.Is<ShortcutTask>(updated => updated.WindowRules.Count == 0),
            CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_Bind_UpdatesHotkeyAndSavesTask()
    {
        var id = new Guid(0x22222222, 0x2222, 0x2222, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22);
        var task = new ShortcutTask { Id = id, Name = "Shortcut", MacroFilePath = "/tmp/a.macro", HotkeyString = "F7" };
        var shortcuts = Substitute.For<IManageShortcut>();
        _ = shortcuts.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>(new ObservableCollection<ShortcutTask> { task }));
        var service = new ShortcutCliService(shortcuts);

        var result = await service.ExecuteAsync(
            new ShortcutCliOptions(ShortcutCliAction.Bind, TaskId: id.ToString(), Hotkey: "Ctrl+Shift+M"),
            CancellationToken.None);

        Assert.True(result.Success);
        _ = await shortcuts.Received(1).UpdateAsync(Arg.Is<ShortcutTask>(updated => updated != null && updated.Id == id && updated.HotkeyString == "Ctrl+Shift+M"));
    }

    [Fact]
    public async Task ExecuteAsync_RemoveMissingTask_ReturnsInvalidArguments()
    {
        var shortcuts = Substitute.For<IManageShortcut>();
        _ = shortcuts.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>(new ObservableCollection<ShortcutTask>()));
        var service = new ShortcutCliService(shortcuts);

        var result = await service.ExecuteAsync(
            new ShortcutCliOptions(ShortcutCliAction.Remove, TaskId: "22222222-2222-2222-2222-222222222222"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_DisableExistingTask_SavesMutation()
    {
        var id = new Guid(0x22222222, 0x2222, 0x2222, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22);
        var shortcuts = Substitute.For<IManageShortcut>();
        _ = shortcuts.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>(new ObservableCollection<ShortcutTask>
        {
            new() { Id = id, Name = "Shortcut", MacroFilePath = "/tmp/a.macro", HotkeyString = "F9" },
        }));
        var service = new ShortcutCliService(shortcuts);

        var result = await service.ExecuteAsync(new ShortcutCliOptions(ShortcutCliAction.Disable, TaskId: id.ToString()), CancellationToken.None);

        Assert.True(result.Success);
        _ = await shortcuts.Received(1).SetEnabledAsync(new TaskRequest(id, Enabled: false), CancellationToken.None);
    }
}
