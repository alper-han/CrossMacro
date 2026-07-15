using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class ProfileCliServiceTests
{
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
        profileManager.ActiveProfile.Returns(active);
        profileManager.Profiles.Returns(profiles);
        return profileManager;
    }
}
