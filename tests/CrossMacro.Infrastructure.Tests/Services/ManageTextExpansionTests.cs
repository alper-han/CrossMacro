using CrossMacro.Application.Automation;

namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ManageTextExpansionTests
{
    [Fact]
    public async Task ListAsync_UsesLoadedStorageSnapshot()
    {
        var storage = Substitute.For<ITextExpansionStorageService>();
        var profileManager = Substitute.For<IProfileManager>();
        _ = profileManager.ActiveProfile.Returns(new ProfileInfo());
        var expected = new List<TextExpansionEntry>
        {
            new(":mail", "me@example.com"),
        };
        storage.IsLoaded.Returns(true);
        storage.GetCurrent().Returns(expected);

        var service = new ManageTextExpansion(storage, profileManager);

        var result = await service.ListAsync();

        _ = result.Should().BeEquivalentTo(expected);
        _ = storage.DidNotReceive().LoadAsync();
    }

    [Fact]
    public async Task ListAsync_ForNonActiveProfile_UsesProfileScopedStoreWithoutReloadingActiveStorage()
    {
        var storage = Substitute.For<ITextExpansionStorageService>();
        var profileStore = Substitute.For<IProfileTextExpansionStore>();
        var profileManager = Substitute.For<IProfileManager>();
        var active = new ProfileInfo { Id = "default", Name = "Default" };
        var target = new ProfileInfo { Id = "work", Name = "Work" };
        _ = profileManager.ActiveProfile.Returns(active);
        _ = profileManager.Profiles.Returns(new[] { active, target });
        _ = profileManager.GetProfileDirectory("work").Returns("/tmp/crossmacro-work");
        _ = profileStore.LoadAsync("/tmp/crossmacro-work", Arg.Any<CancellationToken>())
            .Returns(new List<TextExpansionEntry> { new(":work", "value") });

        var service = new ManageTextExpansion(storage, profileManager, profileStore);

        var result = await service.ListAsync("work");

        _ = result.Should().ContainSingle().Which.Trigger.Should().Be(":work");
        await profileStore.Received(1).LoadAsync("/tmp/crossmacro-work", Arg.Any<CancellationToken>());
        await storage.DidNotReceive().ReloadAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ListAsync_WhenLegacyProfileReloadFails_RestoresActiveProfileState()
    {
        var storage = Substitute.For<ITextExpansionStorageService>();
        var profileManager = Substitute.For<IProfileManager>();
        var active = new ProfileInfo { Id = "default", Name = "Default" };
        var target = new ProfileInfo { Id = "work", Name = "Work" };
        _ = profileManager.ActiveProfile.Returns(active);
        _ = profileManager.Profiles.Returns(new[] { active, target });
        _ = profileManager.GetProfileDirectory("work").Returns("/tmp/crossmacro-work");
        _ = profileManager.GetProfileDirectory("default").Returns("/tmp/crossmacro-default");
        _ = storage.ReloadAsync("/tmp/crossmacro-work")
            .Returns(Task.FromException(new IOException("profile reload failed")));

        var act = async () => await new ManageTextExpansion(storage, profileManager).ListAsync("work");

        _ = await act.Should().ThrowAsync<IOException>();
        await storage.Received(1).ReloadAsync("/tmp/crossmacro-default");
    }
}
