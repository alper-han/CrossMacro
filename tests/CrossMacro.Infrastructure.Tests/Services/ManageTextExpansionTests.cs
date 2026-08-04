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
}
