namespace CrossMacro.Application.Tests.Automation;

public sealed class AutomationTaskAuthorizationTests
{
    [Fact]
    public async Task NestedAuthorization_ComposesPoliciesAndRestoresOuterScopeAfterDenial()
    {
        var authorization = new AutomationTaskAuthorization();
        using var gate = new AutomationTaskMutationGate(authorization);
        var store = Substitute.For<IScheduledTaskStore>();
        _ = store.Tasks.Returns([]);
        using var workflow = new ManageSchedule(Substitute.For<IScheduledTaskOperations>(), store, gate);
        var paths = new List<string>();

        _ = await authorization.RunAsync(path => paths.Add($"outer:{path}"), async () =>
        {
            _ = await workflow.AddAsync(new ScheduledTask { MacroFilePath = "first" }, cancellationToken: CancellationToken.None);
            _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorization.RunAsync(_ => throw new UnauthorizedAccessException("Denied"),
                () => workflow.AddAsync(new ScheduledTask { MacroFilePath = "denied" }, cancellationToken: CancellationToken.None)));
            return await workflow.AddAsync(new ScheduledTask { MacroFilePath = "last" }, cancellationToken: CancellationToken.None);
        });
        _ = await workflow.AddAsync(new ScheduledTask { MacroFilePath = "unscoped" }, cancellationToken: CancellationToken.None);

        Assert.Equal(["outer:first", "outer:denied", "outer:last"], paths, StringComparer.Ordinal);
        await store.Received(3).CommitAsync(Arg.Any<IReadOnlyList<ScheduledTask>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConcurrentRequests_KeepTheirAdmissionPoliciesSeparate()
    {
        var authorization = new AutomationTaskAuthorization();
        using var gate = new AutomationTaskMutationGate(authorization);
        var store = Substitute.For<IShortcutTaskStore>();
        _ = store.Tasks.Returns([]);
        using var workflow = new ManageShortcut(Substitute.For<IShortcutTaskOperations>(), store, gate);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstChecks = 0;
        var secondChecks = 0;
        var first = authorization.RunAsync(path =>
        {
            Assert.Equal("first", path);
            firstChecks++;
        }, async () =>
        {
            await release.Task;
            return await workflow.AddAsync(new ShortcutTask { MacroFilePath = "first" }, cancellationToken: CancellationToken.None);
        });
        var second = authorization.RunAsync(path =>
        {
            Assert.Equal("second", path);
            secondChecks++;
        }, async () =>
        {
            await release.Task;
            return await workflow.AddAsync(new ShortcutTask { MacroFilePath = "second" }, cancellationToken: CancellationToken.None);
        });
        release.SetResult();
        _ = await Task.WhenAll(first, second);

        Assert.Equal(1, firstChecks);
        Assert.Equal(1, secondChecks);
        await store.Received(2).CommitAsync(Arg.Any<IReadOnlyList<ShortcutTask>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CanceledRequest_RestoresAuthorizationBeforeTheNextRequest()
    {
        var authorization = new AutomationTaskAuthorization();
        using var gate = new AutomationTaskMutationGate(authorization);
        var store = Substitute.For<ITriggerTaskStore>();
        _ = store.Tasks.Returns([]);
        using var workflow = new ManageTrigger(Substitute.For<ITriggerTaskOperations>(), store, gate);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => authorization.RunAsync(
            _ => throw new UnauthorizedAccessException("Must not outlive the request"),
            () => workflow.AddAsync(new TriggerTask(), cancellation.Token)));
        _ = await workflow.AddAsync(new TriggerTask(), cancellationToken: CancellationToken.None);

        await store.Received(1).CommitAsync(Arg.Any<IReadOnlyList<TriggerTask>>(), Arg.Any<CancellationToken>());
    }
}
