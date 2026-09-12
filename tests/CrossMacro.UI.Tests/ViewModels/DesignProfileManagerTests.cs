
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class DesignProfileManagerTests
{
    [Fact]
    public void Constructor_ProvidesStableDesignProfilesAndDefaultActiveProfile()
    {
        var manager = new DesignProfileManager();

        _ = manager.Profiles.Select(profile => profile.Id)
            .Should().Equal("default", "dev", "gaming");
        _ = manager.ActiveProfile.Id.Should().Be("default");
    }

    [Fact]
    public async Task CreateProfileAsync_UsesCanonicalLowercaseIdentifier()
    {
        var manager = new DesignProfileManager();

        var profile = await manager.CreateProfileAsync("Mixed Name");

        _ = profile.Id.Should().Be("mixed name");
        _ = profile.Name.Should().Be("Mixed Name");
    }

    [Fact]
    public async Task SwitchProfileAsync_UsesKnownProfileAndFallsBackToDefault()
    {
        var manager = new DesignProfileManager();

        await manager.SwitchProfileAsync("gaming");
        _ = manager.ActiveProfile.Id.Should().Be("gaming");

        await manager.SwitchProfileAsync("missing");
        _ = manager.ActiveProfile.Id.Should().Be("default");
    }
}
