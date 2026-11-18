
using CrossMacro.Application.Profiles;
namespace CrossMacro.Cli.Tests;

public sealed class ProfileCliServiceTests
{
    [Fact]
    public void Constructor_WhenOperationsAreNull_Throws()
    {
        var act = () => new ProfileCliService(null!);

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SwitchAsync_WithDisplayName_ResolvesProfileCaseInsensitively()
    {
        var profileManager = CreateProfileManager();
        var service = CreateService(profileManager);

        var result = await service.SwitchAsync("WORK", CancellationToken.None);

        Assert.True(result.Success);
        await profileManager.Received(1).SwitchProfileAsync("work");
    }

    [Fact]
    public async Task DeleteAsync_WithoutForce_ReturnsInvalidArguments()
    {
        var profileManager = CreateProfileManager();
        var service = CreateService(profileManager);

        var result = await service.DeleteAsync("work", force: false, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Equal("profile delete requires --force.", result.Message);
        Assert.Equal(["Pass --force to confirm profile deletion."], result.Errors);
        await profileManager.DidNotReceive().DeleteProfileAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task RenameAsync_WithDisplayName_UsesStableId()
    {
        var profileManager = CreateProfileManager();
        var service = CreateService(profileManager);

        var result = await service.RenameAsync("Work", "Office", CancellationToken.None);

        Assert.True(result.Success);
        await profileManager.Received(1).RenameProfileAsync("work", "Office");
    }

    private static ProfileCliService CreateService(IProfileManager profileManager) =>
        new(new ProfileOperations(new ManageProfile(profileManager)));

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
        return profileManager;
    }
}
