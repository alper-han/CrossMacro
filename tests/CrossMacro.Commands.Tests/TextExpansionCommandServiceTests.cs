
namespace CrossMacro.Commands.Tests;

public sealed class TextExpansionCommandServiceTests : IDisposable
{
    private readonly ITextExpansionStore _storage = Substitute.For<ITextExpansionStore>();
    private readonly IProfileTextExpansionStore _profileStore = Substitute.For<IProfileTextExpansionStore>();
    private readonly AutomationTaskMutationGate _gate = new();

    public void Dispose() => _gate.Dispose();

    [Fact]
    public async Task AddAsync_WithDuplicateTrigger_ReturnsInvalidArguments()
    {
        _ = _storage.LoadAsync().Returns(new List<TextExpansionEntry> { new(":mail", "old") });
        var service = CreateService();

        var result = await service.AddAsync(
            ":MAIL",
            "new",
            PasteMethod.CtrlV,
            TextInsertionMode.Paste,
            DirectTypingMethod.FastBatch,
            profileIdentifier: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.Outcome);
        await _storage.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task TestAsync_WithExistingTrigger_ReturnsExpansionWithoutSaving()
    {
        _ = _storage.LoadAsync().Returns(new List<TextExpansionEntry> { new(":mail", "me@example.com") });
        var service = CreateService();

        var result = await service.TestAsync(":mail", profileIdentifier: null, CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<TextExpansionTestData>(result.Data);
        Assert.True(data.Found);
        Assert.Equal("me@example.com", data.Expansion?.Replacement);
        await _storage.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task ListAsync_WithOtherProfile_ReadsItsOwnStoreAndLeavesTheActiveStoreAlone()
    {
        _ = _profileStore.LoadAsync("/profiles/work", Arg.Any<CancellationToken>())
            .Returns(new List<TextExpansionEntry> { new(":work", "value") });
        var service = CreateService();

        var result = await service.ListAsync("Work", CancellationToken.None);

        Assert.True(result.Success);
        _ = await _profileStore.Received(1).LoadAsync("/profiles/work", Arg.Any<CancellationToken>());
        _ = _storage.DidNotReceive().LoadAsync();
        await _storage.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task RemoveAsync_WithUnknownTrigger_ReportsTheMissingTriggerWithoutSaving()
    {
        _ = _storage.LoadAsync().Returns(new List<TextExpansionEntry> { new(":mail", "me@example.com") });
        var service = CreateService();

        var result = await service.RemoveAsync(":missing", profileIdentifier: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Text expansion not found.", result.Message);
        await _storage.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetEnabledAsync_WithUnknownProfile_ReportsTheMissingProfile(bool enable)
    {
        _ = _storage.LoadAsync().Returns(new List<TextExpansionEntry> { new(":mail", "me@example.com") });
        var service = CreateService();

        var result = enable
            ? await service.EnableAsync(":mail", "Nope", CancellationToken.None)
            : await service.DisableAsync(":mail", "Nope", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Profile not found.", result.Message);
    }

    private TextExpansionCommandService CreateService() =>
        new(new ManageTextExpansion(_storage, CreateProfileManager(), _profileStore, _gate));

    private static IProfileManager CreateProfileManager()
    {
        var active = new ProfileInfo { Id = "default", Name = "Default", CreatedAt = DateTime.UnixEpoch };
        var profiles = new List<ProfileInfo>
        {
            active,
            new() { Id = "work", Name = "Work", CreatedAt = DateTime.UnixEpoch.AddDays(1) },
        };

        var profileManager = Substitute.For<IProfileManager>();
        _ = profileManager.ActiveProfile.Returns(active);
        _ = profileManager.Profiles.Returns(profiles);
        _ = profileManager.GetProfileDirectory("default").Returns("/profiles/default");
        _ = profileManager.GetProfileDirectory("work").Returns("/profiles/work");
        return profileManager;
    }
}
