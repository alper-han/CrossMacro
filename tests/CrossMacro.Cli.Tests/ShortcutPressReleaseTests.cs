using System.Text.Json;
using CrossMacro.Core.Models.Automation.Shortcuts;
using CrossMacro.Core.Services.Automation;
using CrossMacro.Core.Services.Automation.Shortcuts;

namespace CrossMacro.Cli.Tests;

public sealed class ShortcutPressReleaseTests
{
    [Fact]
    public async Task Handler_AddEditReloadAndSwitchModes_PersistsPairAndReturnsJson()
    {
        using var files = new TempDirectory();
        var press = files.WriteFile("press.macro");
        var release = files.WriteFile("release.macro");
        var store = new JsonStore();
        using var manage = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, store, store.RunTaskAsync);
        var handler = TaskCommandHandlers.Shortcut(TaskCommandTestFactory.Shortcut(manage));
        var added = await ExecuteAsync(handler, ["shortcut", "add", "--name", "Pair", "--macro", press, "--hotkey", "F7", "--press-release", "--release-macro", release]);
        Assert.True(added.Success);
        await store.LoadAsync();
        var task = Assert.Single(store.Tasks);
        Assert.True(task.RunOnPressAndRelease);
        Assert.Equal(release, task.ReleaseMacroFilePath);
        var data = Assert.IsType<ShortcutTaskData>(added.Data);
        Assert.True(data.RunOnPressAndRelease);
        Assert.Equal(release, data.ReleaseMacroFilePath);
        var id = task.Id.ToString();
        Assert.True((await ExecuteAsync(handler, ["shortcut", "edit", id, "--release-macro", press])).Success);
        await store.LoadAsync();
        Assert.Equal(press, Assert.Single(store.Tasks).ReleaseMacroFilePath);
        Assert.True((await ExecuteAsync(handler, ["shortcut", "edit", id, "--run-while-held"])).Success);
        await store.LoadAsync();
        Assert.False(Assert.Single(store.Tasks).RunOnPressAndRelease);
        Assert.True(Assert.Single(store.Tasks).RunWhileHeld);
        Assert.Empty(Assert.Single(store.Tasks).ReleaseMacroFilePath);
        Assert.True((await ExecuteAsync(handler, ["shortcut", "edit", id, "--normal"])).Success);
        await store.LoadAsync();
        Assert.False(Assert.Single(store.Tasks).RunWhileHeld);
        Assert.False(Assert.Single(store.Tasks).LoopEnabled);
    }

    [Theory]
    [InlineData("--loop")]
    [InlineData("--run-while-held")]
    [InlineData("--normal")]
    public void Parser_RejectsConflictingModes(string conflicting)
    {
        var result = CliCommandRouter.Parse(["shortcut", "add", "--name", "Pair", "--macro", "press.macro", "--hotkey", "F7", "--press-release", "--release-macro", "release.macro", conflicting]);
        Assert.False(result.IsSuccess);
        Assert.Contains("cannot", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handler_RejectsUnavailableReleaseBeforeCommit(bool edit)
    {
        using var files = new TempDirectory();
        var press = files.WriteFile("press.macro");
        var release = files.WriteFile("release.macro");
        var store = new JsonStore();
        using var manage = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, store, store.RunTaskAsync);
        var handler = TaskCommandHandlers.Shortcut(TaskCommandTestFactory.Shortcut(manage));
        var original = await manage.AddAsync(new ShortcutTask { Name = "Original", MacroFilePath = press, Hotkeys = { "F7" } }, TestContext.Current.CancellationToken);
        var commits = store.CommitCount;
        string[] operands = edit ? ["shortcut", "edit", original.Id.ToString()] : ["shortcut", "add", "--name", "Pair", "--macro", press, "--hotkey", "F7"];
        var result = await ExecuteAsync(handler, [.. operands, "--press-release", "--release-macro", release + ".missing"]);
        Assert.False(result.Success);
        Assert.Equal(commits, store.CommitCount);
        await store.LoadAsync();
        Assert.False(Assert.Single(store.Tasks).RunOnPressAndRelease);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handler_EnableUnavailablePair_DoesNotPersistEnabledTask(bool missingPress)
    {
        using var files = new TempDirectory();
        var press = files.WriteFile("press.macro");
        var release = files.WriteFile("release.macro");
        var store = new JsonStore();
        using var manage = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, store, store.RunTaskAsync);
        var handler = TaskCommandHandlers.Shortcut(TaskCommandTestFactory.Shortcut(manage));
        var added = await ExecuteAsync(handler, ["shortcut", "add", "--name", "Pair", "--macro", press, "--hotkey", "F7", "--press-release", "--release-macro", release, "--enabled", "false"]);
        Assert.True(added.Success);
        await store.LoadAsync();
        var id = Assert.Single(store.Tasks).Id;
        var commits = store.CommitCount;
        File.Delete(missingPress ? press : release);

        var enabled = await ExecuteAsync(handler, ["shortcut", "enable", id.ToString()]);

        Assert.False(enabled.Success);
        await store.LoadAsync();
        Assert.False(Assert.Single(store.Tasks).IsEnabled);
        Assert.Equal(commits, store.CommitCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SharedMutationBoundary_RejectsUnavailablePairWithoutChangingPersistedTasks(bool update, bool missingPress)
    {
        using var files = new TempDirectory();
        var press = files.WriteFile("press.macro");
        var release = files.WriteFile("release.macro");
        var store = new JsonStore();
        using var manage = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, store, store.RunTaskAsync);
        var task = await manage.AddAsync(new ShortcutTask { Name = "Original", MacroFilePath = press, Hotkeys = { "F7" } }, TestContext.Current.CancellationToken);
        var draft = new ShortcutTask { Id = update ? task.Id : Guid.NewGuid(), MacroFilePath = press, ReleaseMacroFilePath = release, RunOnPressAndRelease = true, Hotkeys = { "F7" } };
        var commits = store.CommitCount;
        File.Delete(missingPress ? press : release);

        await Assert.ThrowsAsync<FileNotFoundException>(() => update
            ? manage.UpdateAsync(draft, TestContext.Current.CancellationToken)
            : manage.AddAsync(draft, TestContext.Current.CancellationToken));

        await store.LoadAsync();
        var persisted = Assert.Single(store.Tasks);
        Assert.Equal(task.Id, persisted.Id);
        Assert.False(persisted.RunOnPressAndRelease);
        Assert.Equal(commits, store.CommitCount);
    }

    [Fact]
    public async Task Handler_DisableUnavailablePair_RemainsAllowed()
    {
        using var files = new TempDirectory();
        var press = files.WriteFile("press.macro");
        var release = files.WriteFile("release.macro");
        var store = new JsonStore();
        using var manage = new ManageRunnableAutomationTasks<ShortcutTask>(ShortcutTaskKind.Instance, store, store.RunTaskAsync);
        var pair = await manage.AddAsync(new ShortcutTask { MacroFilePath = press, ReleaseMacroFilePath = release, RunOnPressAndRelease = true, Hotkeys = { "F7" }, IsEnabled = true }, TestContext.Current.CancellationToken);
        File.Delete(release);
        var handler = TaskCommandHandlers.Shortcut(TaskCommandTestFactory.Shortcut(manage));
        var disabled = await ExecuteAsync(handler, ["shortcut", "disable", pair.Id.ToString()]);
        Assert.True(disabled.Success);
        await store.LoadAsync();
        Assert.False(Assert.Single(store.Tasks).IsEnabled);
    }

    private static async Task<OperationResult> ExecuteAsync(TaskCommandHandler<ShortcutOptions, ShortcutTask, ShortcutCommand> handler, string[] arguments)
    {
        var parsed = CliCommandRouter.Parse(arguments);
        Assert.True(parsed.IsSuccess, parsed.ErrorMessage);
        return await handler.ExecuteAsync(Assert.IsType<ShortcutOptions>(parsed.Options), CancellationToken.None);
    }

    private sealed class JsonStore : IAutomationTaskStore<ShortcutTask>, IShortcutTaskOperations
    {
        private string _json = "[]";
        public int CommitCount { get; private set; }
        public IReadOnlyList<ShortcutTask> Tasks { get; private set; } = [];
        public Task LoadAsync() { Tasks = JsonSerializer.Deserialize<ShortcutTask[]>(_json)!; return Task.CompletedTask; }
        public Task CommitAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken = default) { _json = JsonSerializer.Serialize(tasks); CommitCount++; return Task.CompletedTask; }
        public Task SaveAsync() => throw new NotSupportedException();
        public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
