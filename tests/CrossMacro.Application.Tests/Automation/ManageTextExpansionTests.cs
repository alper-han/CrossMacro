using CrossMacro.Core.Models.Automation.TextExpansion;
using CrossMacro.Core.Models.Profiles;
using CrossMacro.Core.Services.Profiles;

namespace CrossMacro.Application.Tests.Automation;

public sealed class ManageTextExpansionTests : IDisposable
{
    private const string ActiveDirectory = "/profiles/default";
    private const string WorkDirectory = "/profiles/work";

    private readonly ITextExpansionStore _store = Substitute.For<ITextExpansionStore>();
    private readonly IProfileTextExpansionStore _profileStore = Substitute.For<IProfileTextExpansionStore>();
    private readonly IProfileManager _profiles = Substitute.For<IProfileManager>();
    private readonly AutomationTaskMutationGate _gate = new();
    private readonly ManageTextExpansion _manage;

    public ManageTextExpansionTests()
    {
        var active = new ProfileInfo { Id = "default", Name = "Default" };
        var work = new ProfileInfo { Id = "work", Name = "Work" };
        _ = _profiles.ActiveProfile.Returns(active);
        _ = _profiles.Profiles.Returns([active, work]);
        _ = _profiles.GetProfileDirectory("default").Returns(ActiveDirectory);
        _ = _profiles.GetProfileDirectory("work").Returns(WorkDirectory);
        _manage = new ManageTextExpansion(_store, _profiles, _profileStore, _gate);
    }

    public void Dispose() => _gate.Dispose();

    [Fact]
    public async Task List_UsesTheLoadedSnapshotOfTheActiveStore()
    {
        var current = new List<TextExpansionEntry> { new(":mail", "me@example.com") };
        _ = _store.IsLoaded.Returns(returnThis: true);
        _ = _store.GetCurrent().Returns(current);

        var result = await _manage.ListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(current, result);
        _ = _store.DidNotReceive().LoadAsync();
    }

    [Fact]
    public async Task List_LoadsTheActiveStoreWhenItIsNotLoadedYet()
    {
        _ = _store.IsLoaded.Returns(returnThis: false);
        _ = _store.LoadAsync().Returns(new List<TextExpansionEntry> { new(":a", "b") });

        var result = await _manage.ListAsync(cancellationToken: TestContext.Current.CancellationToken);

        _ = Assert.Single(result);
        _ = _store.DidNotReceive().GetCurrent();
    }

    [Theory]
    [InlineData("work")]
    [InlineData("WORK")]
    [InlineData("Work")]
    public async Task List_ForAnotherProfile_ReadsItsOwnStoreAndNeverTouchesTheActiveStore(string identifier)
    {
        _ = _profileStore.LoadAsync(WorkDirectory, Arg.Any<CancellationToken>())
            .Returns(new List<TextExpansionEntry> { new(":work", "value") });

        var result = await _manage.ListAsync(identifier, TestContext.Current.CancellationToken);

        Assert.Equal(":work", Assert.Single(result).Trigger);
        _ = _store.DidNotReceive().LoadAsync();
        _ = _store.DidNotReceive().GetCurrent();
        await _store.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task List_ForTheActiveProfileByName_UsesTheActiveStore()
    {
        _ = _store.IsLoaded.Returns(returnThis: true);
        _ = _store.GetCurrent().Returns(new List<TextExpansionEntry> { new(":a", "b") });

        _ = await _manage.ListAsync("Default", TestContext.Current.CancellationToken);

        _ = _store.Received(1).GetCurrent();
        _ = await _profileStore.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_ForAnUnknownProfile_Throws()
    {
        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() => _manage.ListAsync("nope", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Add_ToAnotherProfile_SavesThroughItsStore()
    {
        _ = _profileStore.LoadAsync(WorkDirectory, Arg.Any<CancellationToken>()).Returns(new List<TextExpansionEntry>());
        var entry = new TextExpansionEntry(":new", "value");

        var added = await _manage.AddAsync(entry, "work", TestContext.Current.CancellationToken);

        Assert.Same(entry, added);
        await _profileStore.Received(1).SaveAsync(
            WorkDirectory,
            Arg.Is<IEnumerable<TextExpansionEntry>>(saved => saved.Single().Trigger == ":new"),
            Arg.Any<CancellationToken>());
        await _store.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task Add_WithADuplicateTriggerIgnoringCase_ThrowsAndSavesNothing()
    {
        _ = _store.LoadAsync().Returns(new List<TextExpansionEntry> { new(":mail", "old") });

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manage.AddAsync(new TextExpansionEntry(":MAIL", "new"), cancellationToken: TestContext.Current.CancellationToken));

        await _store.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task Remove_DropsTheEntryAndSaves()
    {
        _ = _store.LoadAsync().Returns(new List<TextExpansionEntry> { new(":a", "1"), new(":b", "2") });

        var removed = await _manage.RemoveAsync(":A", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(":a", removed.Trigger);
        await _store.Received(1).SaveAsync(Arg.Is<IEnumerable<TextExpansionEntry>>(saved => saved.Single().Trigger == ":b"));
    }

    [Fact]
    public async Task SetEnabled_ForAnUnknownTrigger_ThrowsKeyNotFound()
    {
        _ = _store.LoadAsync().Returns(new List<TextExpansionEntry>());

        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _manage.SetEnabledAsync(":missing", enabled: true, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetEnabled_UpdatesAndSavesTheEntry()
    {
        var entry = new TextExpansionEntry(":a", "1", isEnabled: true);
        _ = _store.LoadAsync().Returns(new List<TextExpansionEntry> { entry });

        var updated = await _manage.SetEnabledAsync(":a", enabled: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(updated.IsEnabled);
        await _store.Received(1).SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task Find_ReturnsNullWhenTheTriggerIsAbsent()
    {
        _ = _store.LoadAsync().Returns(new List<TextExpansionEntry>());

        Assert.Null(await _manage.FindAsync(":missing", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Mutation_WaitsForAProfileSwitchHoldingTheMutationGate()
    {
        _ = _store.LoadAsync().Returns(new List<TextExpansionEntry>());
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var switching = _gate.RunAsync(async () =>
        {
            holding.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
        }, TestContext.Current.CancellationToken);
        await holding.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken);

        var add = _manage.AddAsync(new TextExpansionEntry(":x", "y"), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(add.IsCompleted);
        await _store.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());

        release.SetResult();
        await switching;
        _ = await add;
        await _store.Received(1).SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task Operation_CancelledWhileWaitingForTheGate_DoesNotRun()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var switching = _gate.RunAsync(async () =>
        {
            holding.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
        }, TestContext.Current.CancellationToken);
        await holding.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();

        var list = _manage.ListAsync(cancellationToken: cancellation.Token);
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => list);
        release.SetResult();
        await switching;
        _ = _store.DidNotReceive().LoadAsync();
    }
}
