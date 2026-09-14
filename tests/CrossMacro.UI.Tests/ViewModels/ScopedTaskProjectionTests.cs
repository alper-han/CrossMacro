namespace CrossMacro.UI.Tests.ViewModels;

public sealed class ScopedTaskProjectionTests
{
    [Fact]
    public async Task Refresh_PreservesSameScopeIdentityAndSelectionWhileApplyingMembershipAndOrder()
    {
        var first = new ShortcutTask { Name = "first" };
        var second = new ShortcutTask { Name = "second" };
        var added = new ShortcutTask { Name = "added" };
        var snapshot = new TaskCollectionResult<ShortcutTask>([first, second], scopeGeneration: 5);
        ShortcutTaskEditor? selection = null;
        using var projection = new ScopedTaskProjection<ShortcutTask, ShortcutTaskEditor>(
            _ => Task.FromResult(snapshot), new SerializedUiDispatcher(), static task => task.Id,
            static scope => new ShortcutTaskEditor { ScopeGeneration = scope },
            static (editor, task) => editor.Load(task), () => selection, value => selection = value,
            static () => { });
        await projection.RefreshAsync();
        var firstEditor = projection.Items[0];
        var secondEditor = projection.Items[1];
        selection = secondEditor;

        snapshot = new TaskCollectionResult<ShortcutTask>([second, added, first], scopeGeneration: 5);
        await projection.RefreshAsync();

        Assert.Equal([second.Id, added.Id, first.Id], projection.Items.Select(editor => editor.Id));
        Assert.Same(firstEditor, projection.Items[2]);
        Assert.Same(secondEditor, projection.Items[0]);
        Assert.Same(secondEditor, selection);
        snapshot = new TaskCollectionResult<ShortcutTask>([first, added], scopeGeneration: 5);
        await projection.RefreshAsync();
        Assert.Same(firstEditor, selection);
        Assert.False(projection.TryGetEditor(second.Id, out _));
    }

    [Fact]
    public async Task Refresh_SameIdInNewScope_CreatesFreshEditorAndSelection()
    {
        var id = Guid.NewGuid();
        var snapshot = new TaskCollectionResult<ShortcutTask>([new ShortcutTask { Id = id, Name = "profile A" }], scopeGeneration: 1);
        ShortcutTaskEditor? selection = null;
        using var projection = new ScopedTaskProjection<ShortcutTask, ShortcutTaskEditor>(
            _ => Task.FromResult(snapshot), new SerializedUiDispatcher(), static task => task.Id,
            static scope => new ShortcutTaskEditor { ScopeGeneration = scope },
            static (editor, task) => editor.Load(task), () => selection, value => selection = value,
            static () => { });
        await projection.RefreshAsync();
        var oldEditor = Assert.Single(projection.Items);
        oldEditor.Name = "unsaved A edit";

        snapshot = new TaskCollectionResult<ShortcutTask>([new ShortcutTask { Id = id, Name = "profile B" }], scopeGeneration: 2);
        await projection.RefreshAsync();

        var currentEditor = Assert.Single(projection.Items);
        Assert.NotSame(oldEditor, currentEditor);
        Assert.Same(currentEditor, selection);
        Assert.Equal("profile B", currentEditor.Name);
        Assert.Equal(2, currentEditor.ScopeGeneration);
        Assert.Equal("unsaved A edit", oldEditor.Name);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mutation_SelectsRequestedTaskOnlyWhenProfileStillMatches(bool replaceProfile)
    {
        var first = new ShortcutTask { Name = "first" };
        var second = new ShortcutTask { Name = "second" };
        var snapshot = new TaskCollectionResult<ShortcutTask>([first, second], scopeGeneration: 1);
        ShortcutTaskEditor? selection = null;
        using var projection = new ScopedTaskProjection<ShortcutTask, ShortcutTaskEditor>(
            _ => Task.FromResult(snapshot), new SerializedUiDispatcher(), static task => task.Id,
            static scope => new ShortcutTaskEditor { ScopeGeneration = scope },
            static (editor, task) => editor.Load(task), () => selection, value => selection = value,
            static () => { });
        await projection.RefreshAsync();
        var published = false;
        await projection.ApplyMutationAsync(() =>
        {
            if (replaceProfile)
            {
                snapshot = new TaskCollectionResult<ShortcutTask>([first, second], scopeGeneration: 2);
            }
            return Task.CompletedTask;
        }, projection.RefreshAsync, second.Id, () => published = true);

        Assert.Equal(!replaceProfile, published);
        Assert.Equal(replaceProfile ? first.Id : second.Id, selection?.Id);
    }

    [Fact]
    public async Task Mutation_DisposedWhilePending_DoesNotRefreshOrPublish()
    {
        var loads = 0;
        using var projection = new ScopedTaskProjection<ShortcutTask, ShortcutTaskEditor>(
            _ =>
            {
                loads++;
                return Task.FromResult(new TaskCollectionResult<ShortcutTask>([], scopeGeneration: 1));
            }, new SerializedUiDispatcher(), static task => task.Id,
            static scope => new ShortcutTaskEditor { ScopeGeneration = scope },
            static (editor, task) => editor.Load(task), static () => null, static _ => { }, static () => { });
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var published = false;
        var operation = projection.ApplyMutationAsync(() => pending.Task, projection.RefreshAsync, selectedTaskId: null, () => published = true);
        projection.Dispose();
        pending.SetResult();
        await operation;
        Assert.Equal(0, loads);
        Assert.False(published);
    }
}
