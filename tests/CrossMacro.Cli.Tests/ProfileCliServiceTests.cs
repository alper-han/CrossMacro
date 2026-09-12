
using CrossMacro.Application.Profiles;

namespace CrossMacro.Cli.Tests;

public sealed class ProfileCliServiceTests
{
    [Fact]
    public void Constructor_WhenManageProfileIsNull_Throws()
    {
#pragma warning disable CS8600, CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new ProfileCliService((IManageProfile)null);
#pragma warning restore CS8600, CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WhenManageProfileOrProfileManagerIsNull_Throws()
    {
        var manageProfile = Substitute.For<IManageProfile>();
        var profileManager = CreateProfileManager();

#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var nullManageProfile = () => new ProfileCliService(manageProfile: null, profileManager: profileManager);
        var nullProfileManager = () => new ProfileCliService(manageProfile, profileManager: null);
#pragma warning restore CS8625

        _ = nullManageProfile.Should().Throw<ArgumentNullException>();
        _ = nullProfileManager.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SwitchAsync_WithDisplayName_ResolvesProfileCaseInsensitively()
    {
        var profileManager = CreateProfileManager();
        var service = new ProfileCliService(profileManager);

        var result = await service.SwitchAsync("WORK", CancellationToken.None);

        Assert.True(result.Success);
        await profileManager.Received(1).SwitchProfileAsync("work");
    }

    [Fact]
    public async Task DeleteAsync_WithoutForce_ReturnsInvalidArguments()
    {
        var profileManager = CreateProfileManager();
        var service = new ProfileCliService(profileManager);

        var result = await service.DeleteAsync("work", force: false, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        await profileManager.DidNotReceive().DeleteProfileAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task RenameAsync_WithDisplayName_UsesStableId()
    {
        var profileManager = CreateProfileManager();
        var service = new ProfileCliService(profileManager);

        var result = await service.RenameAsync("Work", "Office", CancellationToken.None);

        Assert.True(result.Success);
        await profileManager.Received(1).RenameProfileAsync("work", "Office");
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
        return profileManager;
    }
}
