
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class DesignProfileManagerTests
{
    [Fact]
    public async Task CreateProfileAsync_UsesCanonicalLowercaseIdentifier()
    {
        var manager = new DesignProfileManager();

        var profile = await manager.CreateProfileAsync("Mixed Name");

        _ = profile.Id.Should().Be("mixed name");
        _ = profile.Name.Should().Be("Mixed Name");
    }
}
