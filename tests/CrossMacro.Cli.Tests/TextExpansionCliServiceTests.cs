
namespace CrossMacro.Cli.Tests;

public sealed class TextExpansionCliServiceTests
{
    [Fact]
    public async Task AddAsync_WithDuplicateTrigger_ReturnsInvalidArguments()
    {
        var storage = Substitute.For<ITextExpansionStorageService>();
        _ = storage.LoadAsync().Returns(new List<TextExpansionEntry> { new(":mail", "old") });
        var service = new TextExpansionCliService(storage, CreateProfileManager());

        var result = await service.AddAsync(
            ":MAIL",
            "new",
            PasteMethod.CtrlV,
            TextInsertionMode.Paste,
            DirectTypingMethod.FastBatch,
            profileIdentifier: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        await storage.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task TestAsync_WithExistingTrigger_ReturnsExpansionWithoutSaving()
    {
        var storage = Substitute.For<ITextExpansionStorageService>();
        _ = storage.LoadAsync().Returns(new List<TextExpansionEntry> { new(":mail", "me@example.com") });
        var service = new TextExpansionCliService(storage, CreateProfileManager());

        var result = await service.TestAsync(":mail", profileIdentifier: null, CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<TextExpansionTestData>(result.Data);
        Assert.True(data.Found);
        Assert.Equal("me@example.com", data.Expansion?.Replacement);
        await storage.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansionEntry>>());
    }

    [Fact]
    public async Task ListAsync_WithProfile_ReloadsProfileThenRestoresActiveProfile()
    {
        var storage = Substitute.For<ITextExpansionStorageService>();
        _ = storage.LoadAsync().Returns(new List<TextExpansionEntry>());
        var profileManager = CreateProfileManager();
        var service = new TextExpansionCliService(storage, profileManager);

        var result = await service.ListAsync("Work", CancellationToken.None);

        Assert.True(result.Success);
        Received.InOrder(() =>
        {
            _ = storage.ReloadAsync("/profiles/work");
            _ = storage.LoadAsync();
            _ = storage.ReloadAsync("/profiles/default");
        });
    }

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
