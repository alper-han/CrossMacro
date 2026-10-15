using System.Text.Json;

namespace CrossMacro.Cli.Tests;

public sealed class ShortcutPressReleaseTests
{

    [Fact]
    public async Task Handler_AddEditReloadAndSwitchModes_PersistsPairAndReturnsJson()
    {
        using var files = new MacroFiles();
        var store = new JsonStore();
        using var manage = new ManageShortcut(store, store);
        var handler = new ShortcutCommandHandler(ShortcutCommandTestFactory.Create(manage));
        var added = await ExecuteAsync(handler, ["shortcut", "add", "--name", "Pair", "--macro", files.Press, "--hotkey", "F7", "--press-release", "--release-macro", files.Release]);
        Assert.True(added.Success);
        await store.LoadAsync();
        var task = Assert.Single(store.Tasks);
        Assert.True(task.RunOnPressAndRelease);
        Assert.Equal(files.Release, task.ReleaseMacroFilePath);
        Assert.Contains("\"runOnPressAndRelease\":true", JsonSerializer.Serialize(added.Data), StringComparison.Ordinal);
        Assert.Contains("releaseMacroFilePath", JsonSerializer.Serialize(added.Data), StringComparison.Ordinal);
        var id = task.Id.ToString();
        Assert.True((await ExecuteAsync(handler, ["shortcut", "edit", id, "--release-macro", files.Press])).Success);
        await store.LoadAsync();
        Assert.Equal(files.Press, Assert.Single(store.Tasks).ReleaseMacroFilePath);
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
        using var files = new MacroFiles();
        var store = new JsonStore();
        using var manage = new ManageShortcut(store, store);
        var handler = new ShortcutCommandHandler(ShortcutCommandTestFactory.Create(manage));
        var original = await manage.AddAsync(new ShortcutTask { Name = "Original", MacroFilePath = files.Press, Hotkeys = { "F7" } }, TestContext.Current.CancellationToken);
        var commits = store.CommitCount;
        string[] operands = edit ? ["shortcut", "edit", original.Id.ToString()] : ["shortcut", "add", "--name", "Pair", "--macro", files.Press, "--hotkey", "F7"];
        var result = await ExecuteAsync(handler, [.. operands, "--press-release", "--release-macro", files.Release + ".missing"]);
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
        using var files = new MacroFiles();
        var store = new JsonStore();
        using var manage = new ManageShortcut(store, store);
        var handler = new ShortcutCommandHandler(ShortcutCommandTestFactory.Create(manage));
        var added = await ExecuteAsync(handler, ["shortcut", "add", "--name", "Pair", "--macro", files.Press, "--hotkey", "F7", "--press-release", "--release-macro", files.Release, "--enabled", "false"]);
        Assert.True(added.Success);
        await store.LoadAsync();
        var id = Assert.Single(store.Tasks).Id;
        var commits = store.CommitCount;
        File.Delete(missingPress ? files.Press : files.Release);

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
        using var files = new MacroFiles();
        var store = new JsonStore();
        using var manage = new ManageShortcut(store, store);
        var task = await manage.AddAsync(new ShortcutTask { Name = "Original", MacroFilePath = files.Press, Hotkeys = { "F7" } }, TestContext.Current.CancellationToken);
        var draft = new ShortcutTask { Id = update ? task.Id : Guid.NewGuid(), MacroFilePath = files.Press, ReleaseMacroFilePath = files.Release, RunOnPressAndRelease = true, Hotkeys = { "F7" } };
        var commits = store.CommitCount;
        File.Delete(missingPress ? files.Press : files.Release);

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
        using var files = new MacroFiles();
        var store = new JsonStore();
        using var manage = new ManageShortcut(store, store);
        var pair = await manage.AddAsync(new ShortcutTask { MacroFilePath = files.Press, ReleaseMacroFilePath = files.Release, RunOnPressAndRelease = true, Hotkeys = { "F7" }, IsEnabled = true }, TestContext.Current.CancellationToken);
        File.Delete(files.Release);
        var handler = new ShortcutCommandHandler(ShortcutCommandTestFactory.Create(manage));
        var disabled = await ExecuteAsync(handler, ["shortcut", "disable", pair.Id.ToString()]);
        Assert.True(disabled.Success);
        await store.LoadAsync();
        Assert.False(Assert.Single(store.Tasks).IsEnabled);
    }

    private static async Task<CliCommandExecutionResult> ExecuteAsync(ShortcutCommandHandler handler, string[] arguments)
    {
        var parsed = CliCommandRouter.Parse(arguments);
        Assert.True(parsed.IsSuccess, parsed.ErrorMessage);
        return await handler.ExecuteAsync(Assert.IsType<ShortcutCliOptions>(parsed.Options), CancellationToken.None);
    }

    private sealed class MacroFiles : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public string Press => Path.Combine(_root, "press.macro");
        public string Release => Path.Combine(_root, "release.macro");
        public MacroFiles() { Directory.CreateDirectory(_root); File.WriteAllText(Press, "fixture"); File.WriteAllText(Release, "fixture"); }
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }

    private sealed class JsonStore : IShortcutTaskStore, IShortcutTaskOperations
    {
        private string _json = "[]";
        public int CommitCount { get; private set; }
        public IReadOnlyList<ShortcutTask> Tasks { get; private set; } = [];
        public Task LoadAsync() { Tasks = JsonSerializer.Deserialize<ShortcutTask[]>(_json)!; return Task.CompletedTask; }
        public Task CommitAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken = default) { _json = JsonSerializer.Serialize(tasks); CommitCount++; return Task.CompletedTask; }
        public Task SaveAsync() => throw new NotSupportedException();
        public void AddTask(ShortcutTask task) => throw new NotSupportedException();
        public void UpdateTask(ShortcutTask task) => throw new NotSupportedException();
        public void RemoveTask(Guid id) => throw new NotSupportedException();
        public void SetTaskEnabled(Guid id, bool enabled) => throw new NotSupportedException();
        public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
